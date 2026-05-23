using System.Text.Json;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Domain.Mapping;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using JoyZoning.Agents;
using JoyZoning.Agents.Hermes;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.SignalR;
using JoyZoning.ControlPlane.Background;
using JoyZoning.ControlPlane.Hubs;

namespace JoyZoning.ControlPlane.Services;

public class OrchestrationService
{
    private readonly IOperatorSessionRepository _sessions;
    private readonly IWorkTaskRepository _tasks;
    private readonly IExecutionRepository _executions;
    private readonly AgentAdapterRegistry _agents;
    private readonly EventIngestor _events;
    private readonly IHubContext<OperatorHub> _hub;
    private readonly KanbanSyncService _kanbanSync;
    private readonly KanbanSyncState _kanbanSyncState;
    private readonly KanbanExecutionOrchestrator _executionOrchestrator;
    private readonly HermesConnectivityService _hermesConnectivity;
    private readonly HermesRunEventConsumer _runConsumer;
    private readonly ConfigService _config;
    private readonly IBroccoliQBridge _broccoliQ;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<OrchestrationService> _logger;
    private readonly string _controlPlaneUrl;

    public OrchestrationService(
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IExecutionRepository executions,
        AgentAdapterRegistry agents,
        EventIngestor events,
        IHubContext<OperatorHub> hub,
        KanbanSyncService kanbanSync,
        KanbanSyncState kanbanSyncState,
        KanbanExecutionOrchestrator executionOrchestrator,
        HermesConnectivityService hermesConnectivity,
        HermesRunEventConsumer runConsumer,
        ConfigService config,
        IBroccoliQBridge broccoliQ,
        IHostEnvironment environment,
        ILogger<OrchestrationService> logger,
        IOptions<ControlPlaneOptions> controlPlaneOptions)
    {
        _sessions = sessions;
        _tasks = tasks;
        _executions = executions;
        _agents = agents;
        _events = events;
        _hub = hub;
        _kanbanSync = kanbanSync;
        _kanbanSyncState = kanbanSyncState;
        _executionOrchestrator = executionOrchestrator;
        _hermesConnectivity = hermesConnectivity;
        _runConsumer = runConsumer;
        _config = config;
        _broccoliQ = broccoliQ;
        _environment = environment;
        _logger = logger;
        _controlPlaneUrl = controlPlaneOptions.Value.ListenUrl;
    }

    private void MirrorWorkTaskToBroccoliQ(WorkTask task)
    {
        if (!_broccoliQ.IsEnabled)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _broccoliQ.MirrorWorkTaskAsync(
                    task.Id,
                    task.Title,
                    task.Description,
                    task.Status.ToString(),
                    priority: (int)task.Risk);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "BroccoliQ work-task mirror failed for {TaskId}", task.Id);
            }
        });
    }

    private async Task<T> DispatchStepAsync<T>(
        string step,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (LeaseOrchestrationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ArgumentException($"Dispatch step '{step}' failed: {ex.Message}", ex);
        }
    }

    private async Task BroadcastSafeAsync(
        string method,
        object? payload,
        CancellationToken cancellationToken)
    {
        if (_environment.IsEnvironment("Testing"))
            return;

        try
        {
            await _hub.Clients.All.SendAsync(method, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR {Method} broadcast skipped", method);
        }
    }

    public async Task<OperatorSession> CreateSessionAsync(
        string name,
        string workspaceRoot,
        string? hermesProfile,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoot = WorkspacePaths.TryNormalize(workspaceRoot, out var norm)
            ? norm
            : workspaceRoot;

        var existing = await _sessions.FindByWorkspaceRootAsync(normalizedRoot, cancellationToken);
        if (existing is not null)
        {
            existing.Name = name;
            if (!string.IsNullOrWhiteSpace(hermesProfile))
                existing.HermesProfile = hermesProfile;
            existing.WorkspaceRoot = normalizedRoot;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _sessions.UpdateAsync(existing, cancellationToken);
            _logger.LogInformation(
                "Reusing operator session {SessionId} for workspace {WorkspaceRoot}",
                existing.Id,
                normalizedRoot);
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var sessionId = Guid.NewGuid();
        var session = new OperatorSession
        {
            Id = sessionId,
            Name = name,
            WorkspaceRoot = normalizedRoot,
            HermesProfile = hermesProfile,
            HermesSessionId = sessionId.ToString(),
            Status = SessionStatus.Idle,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _sessions.CreateAsync(session, cancellationToken);
        await _events.IngestAsync(session.Id, EventSource.JoyZoning, EventTypes.SessionStarted,
            new { session.Id, session.Name, session.WorkspaceRoot }, cancellationToken);
        return session;
    }

    public async Task<WorkTask> CreateTaskAsync(
        Guid sessionId,
        string title,
        string description,
        AgentKind assignedAgent,
        RiskLevel risk,
        CancellationToken cancellationToken = default)
    {
        var opSession = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        var now = DateTimeOffset.UtcNow;
        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            OperatorSessionId = sessionId,
            Title = title,
            Description = description,
            AssignedAgent = assignedAgent,
            Status = WorkTaskStatus.Backlog,
            Risk = risk,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var kanbanId = await _kanbanSync.SyncCreateTaskAsync(task, opSession.WorkspaceRoot, cancellationToken);
        if (!string.IsNullOrEmpty(kanbanId))
        {
            var existing = await FindTaskByHermesKanbanIdForWorkspaceAsync(
                opSession.WorkspaceRoot, kanbanId, cancellationToken);
            if (existing is not null)
            {
                _logger.LogInformation(
                    "Reusing existing task {TaskId} for kanban card {KanbanId} in workspace {WorkspaceRoot}",
                    existing.Id,
                    kanbanId,
                    opSession.WorkspaceRoot);
                return existing;
            }

            task.HermesKanbanTaskId = kanbanId;
        }

        await _tasks.CreateAsync(task, cancellationToken);

        await _events.IngestAsync(task.Id, EventSource.JoyZoning, EventTypes.TaskCreated,
            new { task.Id, task.Title, task.Status }, cancellationToken);
        await BroadcastSafeAsync("OnTaskChanged", TaskDto.From(task), cancellationToken);
        MirrorWorkTaskToBroccoliQ(task);
        return task;
    }

    public async Task<WorkTask> UpdateTaskStatusAsync(
        Guid taskId,
        WorkTaskStatus status,
        StatusChangeActor actor = StatusChangeActor.Human,
        CancellationToken cancellationToken = default)
    {
        await _executionOrchestrator.ValidateTaskStatusChangeAsync(taskId, status, actor, cancellationToken);
        await _tasks.UpdateStatusAsync(taskId, status, cancellationToken);
        var task = await _tasks.GetByIdAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        await _kanbanSync.SyncUpdateStatusAsync(task, cancellationToken);

        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.TaskStatusChanged,
            new { taskId, status }, cancellationToken);
        await BroadcastSafeAsync("OnTaskChanged", TaskDto.From(task), cancellationToken);
        MirrorWorkTaskToBroccoliQ(task);
        return task;
    }

    public async Task<ExecutionSession?> ResumeExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _executions.GetByIdAsync(executionId, cancellationToken);
        if (existing is null || existing.Phase != ExecutionPhase.Interrupted)
            return null;

        return await DispatchTaskAsync(existing.WorkTaskId, humanApprovedCritical: false, cancellationToken);
    }

    public async Task MarkExecutionCancelledAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var execution = await _executions.GetByIdAsync(executionId, cancellationToken);
        if (execution is null)
            return;

        if (!string.IsNullOrWhiteSpace(execution.HermesRunId))
        {
            _runConsumer.StopTracking(execution.HermesRunId);
            var task = await _tasks.GetByIdAsync(execution.WorkTaskId, cancellationToken);
            if (task is not null)
            {
                try
                {
                    await _agents.Get(task.AssignedAgent).StopRunAsync(execution.HermesRunId, cancellationToken);
                }
                catch
                {
                    // Best-effort stop — phase still moves to Cancelled.
                }
            }
        }

        await _executions.UpdatePhaseAsync(executionId, ExecutionPhase.Cancelled, cancellationToken);
    }

    public async Task<ExecutionSession> DispatchTaskAsync(
        Guid taskId,
        bool humanApprovedCritical = false,
        CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Operator session not found");

        var op = new LeaseOperationContext(session.Id, StatusChangeActor.System);
        HandoffPacket handoff;
        var existingLease = await _executionOrchestrator.GetActiveLeaseAsync(taskId, cancellationToken);
        if (existingLease?.Status == ExecutionLeaseStatus.Leased)
        {
            handoff = HandoffPacketBuilder.Deserialize(existingLease.HandoffPacketJson);
            await DispatchStepAsync(
                "RecordDispatchAttempt",
                () => _executionOrchestrator.RecordDispatchAttemptAsync(taskId, op, cancellationToken),
                cancellationToken);
        }
        else if (existingLease is not null
                 && existingLease.Status is not ExecutionLeaseStatus.Blocked
                 && KanbanExecutionRules.IsActiveLease(existingLease))
        {
            throw LeaseOrchestrationException.Conflict(
                $"Task {taskId} already has an active lease ({existingLease.Status}). "
                + "Recover or reopen the lease before dispatching again.");
        }
        else
        {
            var (_, createdHandoff) = await DispatchStepAsync(
                "BeginLease",
                () => _executionOrchestrator.BeginLeaseAsync(taskId, humanApprovedCritical, op, cancellationToken),
                cancellationToken);
            handoff = createdHandoff;

            await DispatchStepAsync(
                "RecordDispatchAttempt",
                () => _executionOrchestrator.RecordDispatchAttemptAsync(taskId, op, cancellationToken),
                cancellationToken);
        }

        await DispatchStepAsync(
            "ApplyHermesProfile",
            async () =>
            {
                await _config.ApplyForOperatorSessionAsync(session, cancellationToken);
                return (object?)null;
            },
            cancellationToken);

        await DispatchStepAsync(
            "EnsureHermesReady",
            async () =>
            {
                await _hermesConnectivity.EnsureReadyForAgentCallsAsync(cancellationToken);
                return (object?)null;
            },
            cancellationToken);

        string runId;
        try
        {
            runId = await DispatchStepAsync(
                "StartAgentRun",
                async () =>
                {
                    var adapter = _agents.Get(task.AssignedAgent);
                    var started = await adapter.StartRunDetailedAsync(new AgentRunRequest
                    {
                        Prompt = HandoffPacketBuilder.ToExecutorPrompt(handoff),
                        SessionId = ResolveHermesSessionId(session),
                        WorkspaceRoot = handoff.WorktreePath,
                    }, cancellationToken);
                    await PersistHermesSessionIdAsync(session, started.SessionId, cancellationToken);
                    return started.RunId;
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            await _executionOrchestrator.RecordDispatchFailureAsync(
                taskId,
                ex.Message,
                revoke: false,
                cancellationToken);
            throw LeaseOrchestrationException.Conflict(
                $"Dispatch failed after lease creation: {ex.Message}");
        }

        await DispatchStepAsync(
            "UpdateDispatch",
            async () =>
            {
                await _tasks.UpdateDispatchAsync(taskId, runId, WorkTaskStatus.InProgress, cancellationToken);
                return (object?)null;
            },
            cancellationToken);

        task = await _tasks.GetByIdAsync(taskId, cancellationToken) ?? task;
        MirrorWorkTaskToBroccoliQ(task);
        await DispatchStepAsync(
            "KanbanSync",
            async () =>
            {
                await _kanbanSync.SyncUpdateStatusAsync(task, cancellationToken);
                return (object?)null;
            },
            cancellationToken);

        var execution = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            WorkTaskId = taskId,
            HermesRunId = runId,
            Objective = task.Title,
            Phase = ExecutionPhase.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };

        await DispatchStepAsync(
            "CreateExecution",
            async () =>
            {
                await _executions.CreateAsync(execution, cancellationToken);
                return (object?)null;
            },
            cancellationToken);

        var runningLease = await DispatchStepAsync(
            "MarkLeaseRunning",
            () => _executionOrchestrator.MarkLeaseRunningAsync(taskId, execution.Id, cancellationToken),
            cancellationToken);

        try
        {
            JoyZoningRuntimeContext.Write(runningLease, _controlPlaneUrl);
        }
        catch
        {
            // Worktree may be on a volume the server cannot write; CLI agent start can refresh.
        }

        await DispatchStepAsync(
            "IngestExecutionStarted",
            async () =>
            {
                await _events.IngestAsync(taskId, EventSource.DietCode, EventTypes.DietCodeExecutionStarted,
                    new { execution.Id, runId, task.Title }, cancellationToken);
                return (object?)null;
            },
            cancellationToken);

        await BroadcastSafeAsync("OnExecutionUpdated", ExecutionDto.From(execution), cancellationToken);

        return execution;
    }

    public async Task<string> SendManagerMessageAsync(
        Guid sessionId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        session.Status = SessionStatus.ManagerActive;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _sessions.UpdateAsync(session, cancellationToken);

        await _config.ApplyForOperatorSessionAsync(session, cancellationToken);
        await _hermesConnectivity.EnsureReadyForAgentCallsAsync(cancellationToken);

        var hermes = _agents.Get(AgentKind.Hermes);
        var preamble =
            "You are Hermes, the project lead and orchestration coordinator. " +
            "Decompose goals into tasks, manage kanban flow, and coordinate DietCode execution. " +
            "Do not write code directly.\n\n";

        var started = await hermes.StartRunDetailedAsync(new AgentRunRequest
        {
            Prompt = preamble + message,
            SessionId = ResolveHermesSessionId(session),
            WorkspaceRoot = session.WorkspaceRoot,
            Role = AgentRole.Manager,
        }, cancellationToken);
        await PersistHermesSessionIdAsync(session, started.SessionId, cancellationToken);
        var runId = started.RunId;

        await _events.IngestAsync(sessionId, EventSource.Hermes, EventTypes.HermesRunStarted,
            new { runId, message }, cancellationToken);

        return runId;
    }

    public Task<KanbanImportResult> ImportKanbanTasksAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        SyncKanbanTwoWayAsync(sessionId, cancellationToken);

    public async Task<KanbanImportResult> SyncKanbanTwoWayAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        var pull = await PullKanbanFromHermesAsync(session, cancellationToken);
        var push = await PushLocalKanbanToHermesAsync(session, cancellationToken);

        var message = pull.AuthFailure
            ? $"Kanban pull failed (auth): {pull.Detail}. Push: {push.Linked} linked, {push.StatusPushed} status(es). Reconnect dashboard token."
            : pull.RemoteCount == 0
                ? $"Pushed {push.Linked} new + {push.StatusPushed} status(es) to Hermes (pull — empty board or no token)."
                : $"Pull: +{pull.Imported} / ~{pull.Updated} (skipped {pull.Skipped}). Push: {push.Linked} linked, {push.StatusPushed} status(es).";

        await _events.IngestAsync(sessionId, EventSource.JoyZoning, EventTypes.KanbanSynced,
            new { pull.Imported, pull.Updated, push.Linked, push.StatusPushed, pull.Outcome }, cancellationToken);

        _kanbanSyncState.LastSyncAt = DateTimeOffset.UtcNow;
        _kanbanSyncState.LastMessage = message;
        _kanbanSyncState.LastOutcome = pull.Outcome;
        if (pull.AuthFailure)
            _kanbanSyncState.LastAuthFailureAt = DateTimeOffset.UtcNow;

        return new KanbanImportResult(pull.Imported, pull.Updated, pull.Skipped, push.Linked + push.StatusPushed, message);
    }

    private async Task<(int Imported, int Updated, int Skipped, int RemoteCount, bool AuthFailure, KanbanSyncOutcome Outcome, string? Detail)> PullKanbanFromHermesAsync(
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        var fetch = await _kanbanSync.FetchBoardTasksAsync(cancellationToken);
        if (fetch.IsAuthFailure)
            return (0, 0, 0, 0, true, fetch.Outcome, fetch.Detail);

        if (fetch.Outcome != KanbanSyncOutcome.Success)
            return (0, 0, 0, 0, false, fetch.Outcome, fetch.Detail);

        var remote = fetch.Tasks;
        if (remote.Count == 0)
            return (0, 0, 0, 0, false, KanbanSyncOutcome.Success, null);

        var canonicalSession = await ResolveCanonicalSessionForWorkspaceAsync(session, cancellationToken)
            ?? session;
        var sessionId = canonicalSession.Id;
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var lastSync = _kanbanSyncState.LastSyncAt;

        foreach (var snap in remote)
        {
            if (string.IsNullOrEmpty(snap.WorkspacePath))
            {
                skipped++;
                continue;
            }

            if (!WorkspacePaths.IsSameOrChildWorkspace(snap.WorkspacePath, session.WorkspaceRoot))
            {
                skipped++;
                continue;
            }

            var agent = snap.Assignee?.Contains("diet", StringComparison.OrdinalIgnoreCase) == true
                ? AgentKind.DietCode
                : AgentKind.Hermes;

            var status = KanbanStatusMapping.FromHermesKanbanStatus(snap.Status);
            var existing = await FindTaskByHermesKanbanIdForWorkspaceAsync(
                session.WorkspaceRoot, snap.Id, cancellationToken);

            if (existing is null)
            {
                var now = DateTimeOffset.UtcNow;
                var task = new WorkTask
                {
                    Id = Guid.NewGuid(),
                    OperatorSessionId = sessionId,
                    HermesKanbanTaskId = snap.Id,
                    Title = snap.Title,
                    Description = snap.Body,
                    AssignedAgent = agent,
                    Status = status,
                    Risk = RiskLevel.Low,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                if (status == WorkTaskStatus.Complete)
                    task.CompletedAt = now;

                await _tasks.CreateAsync(task, cancellationToken);
                await BroadcastSafeAsync("OnTaskChanged", TaskDto.From(task), cancellationToken);
                MirrorWorkTaskToBroccoliQ(task);
                imported++;
            }
            else
            {
                var localWins = lastSync.HasValue && existing.UpdatedAt > lastSync.Value;
                existing.Title = snap.Title;
                existing.Description = snap.Body;
                existing.AssignedAgent = agent;
                if (!localWins)
                {
                    existing.Status = status;
                    if (status == WorkTaskStatus.Complete && existing.CompletedAt is null)
                        existing.CompletedAt = DateTimeOffset.UtcNow;
                }

                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await _tasks.UpdateAsync(existing, cancellationToken);
                await BroadcastSafeAsync("OnTaskChanged", TaskDto.From(existing), cancellationToken);
                MirrorWorkTaskToBroccoliQ(existing);
                updated++;
            }
        }

        return (imported, updated, skipped, remote.Count, false, KanbanSyncOutcome.Success, null);
    }

    private async Task<(int Linked, int StatusPushed)> PushLocalKanbanToHermesAsync(
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        var localTasks = await _tasks.ListBySessionAsync(session.Id, cancellationToken);
        var linked = 0;
        var statusPushed = 0;

        foreach (var task in localTasks)
        {
            if (string.IsNullOrEmpty(task.HermesKanbanTaskId))
            {
                var kanbanId = await _kanbanSync.SyncCreateTaskAsync(task, session.WorkspaceRoot, cancellationToken);
                if (string.IsNullOrEmpty(kanbanId)) continue;

                task.HermesKanbanTaskId = kanbanId;
                task.UpdatedAt = DateTimeOffset.UtcNow;
                await _tasks.UpdateAsync(task, cancellationToken);
                linked++;
            }
            else
            {
                await _kanbanSync.SyncUpdateStatusAsync(task, cancellationToken);
                statusPushed++;
            }
        }

        return (linked, statusPushed);
    }

    private static string ResolveHermesSessionId(OperatorSession session) =>
        string.IsNullOrWhiteSpace(session.HermesSessionId)
            ? session.Id.ToString()
            : session.HermesSessionId;

    private async Task PersistHermesSessionIdAsync(
        OperatorSession session,
        string? returnedSessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(returnedSessionId)
            || string.Equals(returnedSessionId, session.HermesSessionId, StringComparison.Ordinal))
            return;

        session.HermesSessionId = returnedSessionId;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _sessions.UpdateAsync(session, cancellationToken);
    }

    private async Task<WorkTask?> FindTaskByHermesKanbanIdForWorkspaceAsync(
        string workspaceRoot,
        string hermesKanbanTaskId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hermesKanbanTaskId))
            return null;

        var sessions = await _sessions.ListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            if (!WorkspacePaths.EqualsNormalized(session.WorkspaceRoot, workspaceRoot))
                continue;

            var task = await _tasks.GetByHermesKanbanIdAsync(session.Id, hermesKanbanTaskId, cancellationToken);
            if (task is not null)
                return task;
        }

        return null;
    }

    private Task<OperatorSession?> ResolveCanonicalSessionForWorkspaceAsync(
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePaths.TryNormalize(session.WorkspaceRoot, out var norm))
            return Task.FromResult<OperatorSession?>(session);

        return _sessions.FindByWorkspaceRootAsync(norm, cancellationToken);
    }
}

public record KanbanImportResult(int Imported, int Updated, int Skipped, int Pushed, string Message);

public record TaskDto(
    Guid Id,
    Guid OperatorSessionId,
    string Title,
    string Description,
    string AssignedAgent,
    string Status,
    string Risk,
    string? LinkedRunId)
{
    public static TaskDto From(WorkTask t) => new(
        t.Id, t.OperatorSessionId, t.Title, t.Description,
        t.AssignedAgent.ToString(), t.Status.ToString(), t.Risk.ToString(), t.LinkedRunId);
}

public record ExecutionDto(
    Guid Id,
    Guid WorkTaskId,
    string HermesRunId,
    string Objective,
    string Phase,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt)
{
    public static ExecutionDto From(ExecutionSession e) => new(
        e.Id, e.WorkTaskId, e.HermesRunId, e.Objective,
        e.Phase.ToString(), e.StartedAt, e.EndedAt);
}
