using System.Text.Json;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Domain.Mapping;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Orchestration;
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
        _controlPlaneUrl = controlPlaneOptions.Value.ListenUrl;
    }

    public async Task<OperatorSession> CreateSessionAsync(
        string name,
        string workspaceRoot,
        string? hermesProfile,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new OperatorSession
        {
            Id = Guid.NewGuid(),
            Name = name,
            WorkspaceRoot = workspaceRoot,
            HermesProfile = hermesProfile,
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

        await _tasks.CreateAsync(task, cancellationToken);

        var opSession = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        if (opSession is not null)
        {
            var kanbanId = await _kanbanSync.SyncCreateTaskAsync(task, opSession.WorkspaceRoot, cancellationToken);
            if (!string.IsNullOrEmpty(kanbanId))
            {
                task.HermesKanbanTaskId = kanbanId;
                await _tasks.UpdateAsync(task, cancellationToken);
            }
        }

        await _events.IngestAsync(task.Id, EventSource.JoyZoning, EventTypes.TaskCreated,
            new { task.Id, task.Title, task.Status }, cancellationToken);
        await _hub.Clients.All.SendAsync("OnTaskChanged", TaskDto.From(task), cancellationToken);
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
        await _hub.Clients.All.SendAsync("OnTaskChanged", TaskDto.From(task), cancellationToken);
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
        var (_, handoff) = await _executionOrchestrator.BeginLeaseAsync(
            taskId, humanApprovedCritical, op, cancellationToken);

        await _executionOrchestrator.RecordDispatchAttemptAsync(taskId, op, cancellationToken);

        string runId;
        try
        {
            var adapter = _agents.Get(task.AssignedAgent);
            runId = await adapter.StartRunAsync(new AgentRunRequest
            {
                Prompt = HandoffPacketBuilder.ToExecutorPrompt(handoff),
                SessionId = session.HermesSessionId,
                WorkspaceRoot = handoff.WorktreePath,
            }, cancellationToken);
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

        await _tasks.UpdateDispatchAsync(taskId, runId, WorkTaskStatus.InProgress, cancellationToken);
        task = await _tasks.GetByIdAsync(taskId, cancellationToken) ?? task;
        await _kanbanSync.SyncUpdateStatusAsync(task, cancellationToken);

        var execution = new ExecutionSession
        {
            Id = Guid.NewGuid(),
            WorkTaskId = taskId,
            HermesRunId = runId,
            Objective = task.Title,
            Phase = ExecutionPhase.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };

        await _executions.CreateAsync(execution, cancellationToken);
        var runningLease = await _executionOrchestrator.MarkLeaseRunningAsync(taskId, execution.Id, cancellationToken);
        try
        {
            JoyZoningRuntimeContext.Write(runningLease, _controlPlaneUrl);
        }
        catch
        {
            // Worktree may be on a volume the server cannot write; CLI agent start can refresh.
        }

        await _events.IngestAsync(taskId, EventSource.DietCode, EventTypes.DietCodeExecutionStarted,
            new { execution.Id, runId, task.Title }, cancellationToken);

        await _hub.Clients.All.SendAsync("OnExecutionUpdated",
            ExecutionDto.From(execution), cancellationToken);

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

        var hermes = _agents.Get(AgentKind.Hermes);
        var preamble =
            "You are Hermes, the project lead and orchestration coordinator. " +
            "Decompose goals into tasks, manage kanban flow, and coordinate DietCode execution. " +
            "Do not write code directly.\n\n";

        var runId = await hermes.StartRunAsync(new AgentRunRequest
        {
            Prompt = preamble + message,
            SessionId = session.HermesSessionId,
            WorkspaceRoot = session.WorkspaceRoot,
            Role = AgentRole.Manager,
        }, cancellationToken);

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

        var message = pull.RemoteCount == 0
            ? $"Pushed {push.Linked} new + {push.StatusPushed} status(es) to Hermes (pull skipped — no board data)."
            : $"Pull: +{pull.Imported} / ~{pull.Updated} (skipped {pull.Skipped}). Push: {push.Linked} linked, {push.StatusPushed} status(es).";

        await _events.IngestAsync(sessionId, EventSource.JoyZoning, EventTypes.KanbanSynced,
            new { pull.Imported, pull.Updated, push.Linked, push.StatusPushed }, cancellationToken);

        _kanbanSyncState.LastSyncAt = DateTimeOffset.UtcNow;
        _kanbanSyncState.LastMessage = message;

        return new KanbanImportResult(pull.Imported, pull.Updated, pull.Skipped, push.Linked + push.StatusPushed, message);
    }

    private async Task<(int Imported, int Updated, int Skipped, int RemoteCount)> PullKanbanFromHermesAsync(
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        var remote = await _kanbanSync.FetchBoardTasksAsync(cancellationToken);
        if (remote.Count == 0)
            return (0, 0, 0, 0);

        var sessionId = session.Id;
        var workspaceNorm = Path.GetFullPath(session.WorkspaceRoot).TrimEnd(Path.DirectorySeparatorChar);
        var imported = 0;
        var updated = 0;
        var skipped = 0;
        var lastSync = _kanbanSyncState.LastSyncAt;

        foreach (var snap in remote)
        {
            if (!string.IsNullOrEmpty(snap.WorkspacePath))
            {
                try
                {
                    var taskRoot = Path.GetFullPath(snap.WorkspacePath).TrimEnd(Path.DirectorySeparatorChar);
                    if (!taskRoot.Equals(workspaceNorm, StringComparison.OrdinalIgnoreCase)
                        && !taskRoot.StartsWith(workspaceNorm + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                        continue;
                    }
                }
                catch
                {
                    skipped++;
                    continue;
                }
            }

            var agent = snap.Assignee?.Contains("diet", StringComparison.OrdinalIgnoreCase) == true
                ? AgentKind.DietCode
                : AgentKind.Hermes;

            var status = KanbanStatusMapping.FromHermesKanbanStatus(snap.Status);
            var existing = await _tasks.GetByHermesKanbanIdAsync(sessionId, snap.Id, cancellationToken);

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
                await _hub.Clients.All.SendAsync("OnTaskChanged", TaskDto.From(task), cancellationToken);
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
                await _hub.Clients.All.SendAsync("OnTaskChanged", TaskDto.From(existing), cancellationToken);
                updated++;
            }
        }

        return (imported, updated, skipped, remote.Count);
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
