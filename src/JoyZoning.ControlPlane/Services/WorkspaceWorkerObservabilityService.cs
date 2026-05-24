using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Read model for workspace workers and merge queue (JSDP canonical workspace).</summary>
public sealed class WorkspaceWorkerObservabilityService
{
    private readonly IOperatorSessionRepository _sessions;
    private readonly IWorkTaskRepository _tasks;
    private readonly IExecutionLeaseRepository _leases;
    private readonly IExecutionRepository _executions;
    private readonly WorkerMergeObservabilityBuilder _mergeBuilder;
    private readonly AuthorityAutopilotService _autopilot;
    private readonly AuthorityOptions _authorityOptions;
    private readonly WorkspaceParallelismOptions _parallelism;

    public WorkspaceWorkerObservabilityService(
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IExecutionLeaseRepository leases,
        IExecutionRepository executions,
        IOptions<AuthorityOptions> authorityOptions,
        IOptions<WorkspaceParallelismOptions> parallelism,
        WorkerMergeObservabilityBuilder mergeBuilder,
        AuthorityAutopilotService autopilot)
    {
        _sessions = sessions;
        _tasks = tasks;
        _leases = leases;
        _executions = executions;
        _authorityOptions = authorityOptions.Value;
        _parallelism = parallelism.Value;
        _mergeBuilder = mergeBuilder;
        _autopilot = autopilot;
    }

    public async Task<ParallelWorkersResponse> GetParallelWorkersAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        return await BuildForWorkspaceAsync(session, cancellationToken);
    }

    public async Task<OperatorDecisionPreflight> GetDecisionPreflightAsync(
        Guid sessionId,
        Guid executionSessionId,
        string action,
        CancellationToken cancellationToken = default)
    {
        if (!OperatorDecisionActions.TryParse(action, out var parsedAction))
            throw new InvalidOperationException("action must be accept, revoke, or inspect (approve is a legacy alias for accept).");

        var workers = await GetParallelWorkersAsync(sessionId, cancellationToken);
        var worker = workers.Workers.FirstOrDefault(w => w.ExecutionSessionId == executionSessionId)
            ?? throw new InvalidOperationException($"Worker execution {executionSessionId} not found in session.");

        return OperatorDecisionSafety.BuildPreflight(
            workers.SessionId,
            parsedAction,
            worker,
            _parallelism.LargeChangeSetFileThreshold);
    }

    public async Task<MergeQueueResponse> GetMergeQueueAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var workers = await GetParallelWorkersAsync(sessionId, cancellationToken);
        return WorkerMergeObservabilityBuilder.BuildQueue(
            workers.SessionId,
            workers.SessionWorkspaceRoot,
            workers.Workers,
            workers.Warnings);
    }

    private async Task<ParallelWorkersResponse> BuildForWorkspaceAsync(
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        var sessionRoot = Path.GetFullPath(session.WorkspaceRoot);
        var activeCount = await _leases.CountActiveForWorkspaceAsync(session.WorkspaceRoot, cancellationToken);
        var parallelActive = activeCount > 1;

        var allTasks = await _tasks.ListByWorkspaceRootAsync(session.WorkspaceRoot, cancellationToken);
        var taskById = allTasks.ToDictionary(t => t.Id);

        var workspaceLeases = (await _leases.ListForWorkspaceAsync(session.WorkspaceRoot, cancellationToken))
            .Where(l =>
                KanbanExecutionRules.ActiveLeaseStatuses.Contains(l.Status)
                || KanbanExecutionRules.IsTerminal(l.Status))
            .ToList();

        var warnings = new List<WorkerObservabilityWarning>();
        if (parallelActive && session.IsBoundedRoleSession)
        {
            warnings.Add(new WorkerObservabilityWarning(
                "jsdp_parallel_active",
                "JSDP expects one active role at a time; finish accept-merge before dispatching the next role.",
                null,
                null));
        }

        var readyPathData = new List<(ExecutionLease Lease, IReadOnlyList<string> Paths)>();
        foreach (var lease in workspaceLeases.Where(l => l.Status == ExecutionLeaseStatus.ReadyForReview))
        {
            var paths = await WorkerMergeObservabilityBuilder.ResolveChangedPathsForLeaseAsync(
                lease,
                sessionRoot,
                cancellationToken);
            readyPathData.Add((lease, paths));
        }

        var readyFileMap = WorkerMergeObservabilityBuilder.BuildReadyWorkerFileMap(readyPathData);
        var workers = new List<ParallelWorkerEntry>();
        var readyForReviewCount = readyPathData.Count;

        foreach (var lease in workspaceLeases)
        {
            if (!taskById.TryGetValue(lease.WorkTaskId, out var task))
                continue;

            workers.Add(await BuildWorkerEntryAsync(
                lease,
                task,
                session,
                sessionRoot,
                readyFileMap,
                readyForReviewCount,
                cancellationToken));
        }

        var profile = _autopilot.ResolveProfile(session);
        var sessionAuthority = new SessionAuthoritySnapshot(
            profile.ToString(),
            AuthorityProfileLabels.ToLabel(profile),
            _authorityOptions.AutopilotEnabled);

        return new ParallelWorkersResponse(
            SessionId: session.Id,
            SessionWorkspaceRoot: sessionRoot,
            UpdatedAt: DateTimeOffset.UtcNow,
            ParallelActive: parallelActive,
            Protocol: JsdpProtocol.ProtocolId,
            Authority: sessionAuthority,
            Workers: workers.OrderBy(w => w.TaskTitle).ToList(),
            Warnings: warnings);
    }

    private async Task<ParallelWorkerEntry> BuildWorkerEntryAsync(
        ExecutionLease lease,
        WorkTask task,
        OperatorSession session,
        string sessionRoot,
        IReadOnlyDictionary<Guid, HashSet<string>> readyFileMap,
        int readyForReviewPeerCount,
        CancellationToken cancellationToken)
    {
        string? hermesSessionId = null;
        ExecutionPhase? executionPhase = null;
        if (lease.ExecutionSessionId is { } execId && execId != Guid.Empty)
        {
            var execution = await _executions.GetByIdAsync(execId, cancellationToken);
            hermesSessionId = execution?.HermesSessionId;
            executionPhase = execution?.Phase;
        }

        var (mergeState, readiness, conflict) = await _mergeBuilder.EnrichAsync(
            lease,
            task,
            sessionRoot,
            executionPhase,
            readyFileMap,
            cancellationToken);

        var entry = new ParallelWorkerEntry(
            TaskId: task.Id,
            TaskTitle: task.Title,
            ExecutionSessionId: lease.ExecutionSessionId,
            LeaseId: lease.Id,
            HermesSessionId: hermesSessionId,
            WorkspacePath: lease.WorktreePath,
            KanbanRevision: task.KanbanRevision,
            KanbanPushedRevision: task.KanbanPushedRevision,
            KanbanStatus: task.Status.ToString(),
            LeaseStatus: lease.Status.ToString(),
            MergeState: WorkerMergeStateNames.ToApiString(mergeState),
            MergeReadiness: readiness,
            MergeConflict: conflict,
            DecisionSummary: null!,
            ApproveGuardrails: null!,
            RevokeGuardrails: null!,
            RecommendedModeSlug: string.Empty,
            AvailableModeTransitions: Array.Empty<ModeTransitionHint>(),
            AuthorityProfileSlug: string.Empty,
            Authority: null);

        var (summary, approve, revoke) = OperatorDecisionSafety.BuildForWorker(
            entry,
            _parallelism.LargeChangeSetFileThreshold);
        var withDecision = entry with
        {
            DecisionSummary = summary,
            ApproveGuardrails = approve,
            RevokeGuardrails = revoke,
        };
        var modeHints = OperationalModeNavigation.ForWorker(withDecision);

        AuthorityAutopilotDecision? authority = null;
        var profileSlug = string.Empty;
        var profile = _autopilot.ResolveProfile(session);
        profileSlug = profile.ToString();
        var mergeSnapshot = AuthorityAutopilotMergeContextBuilder.FromWorkerEntry(
            withDecision.MergeState,
            withDecision.MergeReadiness,
            withDecision.MergeConflict,
            readyForReviewPeerCount);
        var evaluated = _autopilot.EvaluateLease(lease, task, session, mergeSnapshot);
        authority = new AuthorityAutopilotDecision(
            evaluated.Profile,
            evaluated.RiskLevel,
            evaluated.AutoAcceptAllowed,
            evaluated.NeedsHumanReview,
            evaluated.ReasonCodes,
            evaluated.HumanMessages,
            AutoAcceptedAt: null,
            WasAutoAccepted: false);

        return withDecision with
        {
            RecommendedModeSlug = modeHints.RecommendedModeSlug,
            AvailableModeTransitions = modeHints.AvailableTransitions,
            AuthorityProfileSlug = profileSlug,
            Authority = authority,
        };
    }
}
