using System.Text.Json;
using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Read model for parallel live mirrors (index.json, registry, disk meta, leases).</summary>
public sealed class WorkspaceLiveMirrorObservabilityService
{
    private readonly IOperatorSessionRepository _sessions;
    private readonly IWorkTaskRepository _tasks;
    private readonly IExecutionLeaseRepository _leases;
    private readonly IExecutionRepository _executions;
    private readonly WorkspaceOptions _workspace;
    private readonly WorkspaceParallelismOptions _parallelism;
    private readonly WorkspaceLiveMirrorRegistry _registry;
    private readonly WorkerMergeObservabilityBuilder _mergeBuilder;

    public WorkspaceLiveMirrorObservabilityService(
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IExecutionLeaseRepository leases,
        IExecutionRepository executions,
        IOptions<WorkspaceOptions> workspace,
        IOptions<WorkspaceParallelismOptions> parallelism,
        WorkspaceLiveMirrorRegistry registry,
        WorkerMergeObservabilityBuilder mergeBuilder)
    {
        _sessions = sessions;
        _tasks = tasks;
        _leases = leases;
        _executions = executions;
        _workspace = workspace.Value;
        _parallelism = parallelism.Value;
        _registry = registry;
        _mergeBuilder = mergeBuilder;
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
            throw new InvalidOperationException("action must be approve, revoke, or inspect.");

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

    public async Task WriteIndexFileAsync(
        OperatorSession session,
        int activeLeasesInWorkspace,
        CancellationToken cancellationToken = default)
    {
        var response = await BuildForWorkspaceAsync(session, cancellationToken);
        var indexDir = Path.Combine(Path.GetFullPath(session.WorkspaceRoot), ".joyzoning", "live");
        Directory.CreateDirectory(indexDir);
        var indexPath = Path.Combine(indexDir, "index.json");

        var body = new
        {
            updatedAt = response.UpdatedAt,
            sessionWorkspaceRoot = response.SessionWorkspaceRoot,
            sessionId = response.SessionId,
            parallelActive = response.ParallelActive,
            liveMirrorMode = response.LiveMirrorMode,
            disableSharedSessionRootMirrorWhenParallel = response.DisableSharedSessionRootMirrorWhenParallel,
            sharedSessionRootMirroringSuppressed = response.SharedSessionRootMirroringSuppressed,
            sessionRootIsCanonicalLiveState = response.SessionRootIsCanonicalLiveState,
            canonicalLiveStateHint = response.CanonicalLiveStateHint,
            indexJsonPath = response.IndexJsonPath,
            warnings = response.Warnings.Select(w => new { w.Code, w.Message, w.LeaseId, w.TaskId }),
            mirrors = response.Workers.Select(w => new
            {
                taskId = w.TaskId,
                taskTitle = w.TaskTitle,
                executionSessionId = w.ExecutionSessionId,
                leaseId = w.LeaseId,
                hermesSessionId = w.HermesSessionId,
                mirrorRoot = w.LiveMirrorPath,
                liveMarkdown = w.LiveMarkdownPath,
                healthState = w.HealthState,
                lifecycleStatus = w.LifecycleStatus,
                lastMirroredAt = w.LastMirroredAt,
                kanbanRevision = w.KanbanRevision,
                kanbanPushedRevision = w.KanbanPushedRevision,
                kanbanStatus = w.KanbanStatus,
                leaseStatus = w.LeaseStatus,
                worktreePath = w.WorktreePath,
                isSharedSessionRootMirror = w.IsSharedSessionRootMirror,
                mergeState = w.MergeState,
                mergeReadiness = w.MergeReadiness is null
                    ? null
                    : new
                    {
                        executionSessionId = w.MergeReadiness.ExecutionSessionId,
                        w.MergeReadiness.WorktreePath,
                        w.MergeReadiness.LiveMirrorPath,
                        w.MergeReadiness.MergeTargetWorkspaceRoot,
                        w.MergeReadiness.MergeTargetBranch,
                        w.MergeReadiness.HeadCommit,
                        w.MergeReadiness.BaseCommit,
                        w.MergeReadiness.IsDirty,
                        w.MergeReadiness.ChangedFilesCount,
                        w.MergeReadiness.ChangedFilesSummary,
                        w.MergeReadiness.VerificationPassed,
                        w.MergeReadiness.TestsRun,
                        w.MergeReadiness.VerificationSummary,
                    },
                mergeConflict = w.MergeConflict is null
                    ? null
                    : new
                    {
                        w.MergeConflict.Category,
                        w.MergeConflict.Reason,
                        w.MergeConflict.ConflictFiles,
                    },
                decisionSummary = OperatorDecisionApiMapper.MapSummary(w.DecisionSummary),
                approveBlocked = w.ApproveGuardrails.Blocked,
                revokeRequiresAck = w.RevokeGuardrails.RequiresAcknowledgement,
                registryCollision = w.RegistryCollision is null
                    ? null
                    : new
                    {
                        w.RegistryCollision.OccupyingLeaseId,
                        w.RegistryCollision.OccupyingTaskId,
                        w.RegistryCollision.OccupyingMirrorRoot,
                    },
            }),
        };

        await File.WriteAllTextAsync(
            indexPath,
            JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private async Task<ParallelWorkersResponse> BuildForWorkspaceAsync(
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        var sessionRoot = Path.GetFullPath(session.WorkspaceRoot);
        var activeCount = await _leases.CountActiveForWorkspaceAsync(session.WorkspaceRoot, cancellationToken);
        var parallelActive = activeCount > 1;
        var sharedSuppressed = parallelActive
            && _parallelism.DisableSharedSessionRootMirrorWhenParallel
            && _parallelism.LiveMirrorMode == LiveMirrorMode.SharedSessionRoot;

        var allTasks = await _tasks.ListByWorkspaceRootAsync(session.WorkspaceRoot, cancellationToken);
        var taskById = allTasks.ToDictionary(t => t.Id);

        var allLeases = await _leases.ListForWorkspaceAsync(session.WorkspaceRoot, cancellationToken);
        var workspaceLeases = allLeases
            .Where(l =>
                KanbanExecutionRules.ActiveLeaseStatuses.Contains(l.Status)
                || KanbanExecutionRules.IsTerminal(l.Status))
            .ToList();

        var warnings = new List<MirrorObservabilityWarning>();
        var readyPathData = new List<(ExecutionLease Lease, IReadOnlyList<string> Paths)>();
        foreach (var lease in workspaceLeases.Where(l => l.Status == ExecutionLeaseStatus.ReadyForReview))
        {
            var paths = await ResolveChangedPathsForLeaseAsync(lease, sessionRoot, cancellationToken);
            readyPathData.Add((lease, paths));
        }

        var readyFileMap = WorkerMergeObservabilityBuilder.BuildReadyWorkerFileMap(readyPathData);
        var workers = new List<ParallelWorkerMirrorEntry>();

        foreach (var lease in workspaceLeases)
        {
            if (!taskById.TryGetValue(lease.WorkTaskId, out var task))
                continue;

            workers.Add(await BuildWorkerEntryAsync(
                lease,
                task,
                sessionRoot,
                activeCount,
                readyFileMap,
                warnings,
                cancellationToken));
        }

        MergeDiskMirrors(sessionRoot, workers, warnings);

        var sessionRootCanonical = !parallelActive
            && _parallelism.LiveMirrorMode == LiveMirrorMode.SharedSessionRoot
            && _workspace.MirrorToSessionRoot;

        var hint = parallelActive || _parallelism.LiveMirrorMode != LiveMirrorMode.SharedSessionRoot
            ? "Per-worker live state lives under .joyzoning/live/ — session root is not canonical during parallel runs."
            : workers.Count == 1 && workers[0].IsSharedSessionRootMirror
                ? "Single worker — session root mirror is canonical."
                : "Open .joyzoning/live/index.json for mirror locations.";

        if (parallelActive && _parallelism.LiveMirrorMode == LiveMirrorMode.PerExecution)
        {
            warnings.Add(new MirrorObservabilityWarning(
                "parallel_isolated",
                "Parallel execution — each worker has an isolated live mirror (PerExecution).",
                null,
                null));
        }

        if (sharedSuppressed)
        {
            warnings.Add(new MirrorObservabilityWarning(
                "shared_root_guard",
                "Shared session-root mirroring suppressed while multiple active leases are running.",
                null,
                null));
        }

        return new ParallelWorkersResponse(
            SessionId: session.Id,
            SessionWorkspaceRoot: sessionRoot,
            UpdatedAt: DateTimeOffset.UtcNow,
            ParallelActive: parallelActive,
            LiveMirrorMode: _parallelism.LiveMirrorMode.ToString(),
            DisableSharedSessionRootMirrorWhenParallel: _parallelism.DisableSharedSessionRootMirrorWhenParallel,
            SharedSessionRootMirroringSuppressed: sharedSuppressed,
            SessionRootIsCanonicalLiveState: sessionRootCanonical,
            CanonicalLiveStateHint: hint,
            IndexJsonPath: Path.Combine(sessionRoot, ".joyzoning", "live", "index.json"),
            Workers: workers.OrderBy(w => w.TaskTitle).ToList(),
            Warnings: warnings);
    }

    private async Task<ParallelWorkerMirrorEntry> BuildWorkerEntryAsync(
        ExecutionLease lease,
        WorkTask task,
        string sessionRoot,
        int activeLeasesInWorkspace,
        IReadOnlyDictionary<Guid, HashSet<string>> readyFileMap,
        List<MirrorObservabilityWarning> warnings,
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

        var outcome = _registry.GetOutcome(lease.Id);
        MirrorMetaOnDisk? meta = null;
        string? mirrorPath = outcome?.MirrorRoot;
        var collision = (MirrorCollisionInfo?)null;
        WorkspaceLiveMirrorTargetResolver.MirrorTarget? target = null;

        if (WorkspaceLiveMirrorTargetResolver.TryResolve(
                lease,
                sessionRoot,
                _workspace.MirrorToSessionRoot,
                _parallelism,
                activeLeasesInWorkspace,
                out var resolved,
                out _))
        {
            target = resolved;
            mirrorPath ??= target.MirrorRoot;
            meta = ReadMirrorMeta(target.MirrorRoot);

            if (_registry.HasCollision(target.MirrorRoot, lease.Id))
            {
                var occupant = _registry.GetOccupant(target.MirrorRoot);
                collision = occupant is null
                    ? null
                    : new MirrorCollisionInfo(
                        occupant.LeaseId,
                        occupant.TaskId,
                        occupant.MirrorRoot);
                warnings.Add(new MirrorObservabilityWarning(
                    "collision",
                    $"Lease {lease.Id} cannot mirror to {target.MirrorRoot} — held by lease {occupant?.LeaseId}.",
                    lease.Id,
                    task.Id));
            }
        }

        if (mirrorPath is not null && meta is null)
            meta = ReadMirrorMeta(mirrorPath);

        var health = ResolveHealth(outcome, meta, lease, target?.IsSharedSessionRoot == true, activeLeasesInWorkspace);

        if (health == LiveMirrorHealthState.SkippedParallelSharedRootGuard)
        {
            warnings.Add(new MirrorObservabilityWarning(
                "skipped_parallel_shared_root_guard",
                outcome?.Detail ?? "Shared session-root mirror skipped for this worker.",
                lease.Id,
                task.Id));
        }

        if (health == LiveMirrorHealthState.SkippedCollision)
        {
            warnings.Add(new MirrorObservabilityWarning(
                "skipped_collision",
                outcome?.Detail ?? "Mirror path owned by another active lease.",
                lease.Id,
                task.Id));
        }

        if (health == LiveMirrorHealthState.FailedCopy)
        {
            warnings.Add(new MirrorObservabilityWarning(
                "failed_copy",
                outcome?.Detail ?? "Last mirror copy failed.",
                lease.Id,
                task.Id));
        }

        var lifecycle = meta?.LifecycleStatus ?? LiveMirrorHealthStateNames.ToApiString(health);

        var (mergeState, readiness, conflict) = await _mergeBuilder.EnrichAsync(
            lease,
            task,
            sessionRoot,
            mirrorPath,
            executionPhase,
            lifecycle,
            meta?.MergeState,
            readyFileMap,
            cancellationToken);

        return EnrichWorkerEntry(new ParallelWorkerMirrorEntry(
            TaskId: task.Id,
            TaskTitle: task.Title,
            ExecutionSessionId: lease.ExecutionSessionId,
            LeaseId: lease.Id,
            HermesSessionId: hermesSessionId,
            LiveMirrorPath: mirrorPath,
            LiveMarkdownPath: mirrorPath is null
                ? null
                : Path.Combine(mirrorPath, _workspace.LiveStatusFileName),
            HealthState: LiveMirrorHealthStateNames.ToApiString(health),
            LifecycleStatus: lifecycle,
            LastMirroredAt: meta?.LastMirroredAt ?? outcome?.RecordedAt,
            KanbanRevision: task.KanbanRevision,
            KanbanPushedRevision: task.KanbanPushedRevision,
            KanbanStatus: task.Status.ToString(),
            LeaseStatus: lease.Status.ToString(),
            WorktreePath: lease.WorktreePath,
            IsSharedSessionRootMirror: target?.IsSharedSessionRoot == true,
            RegistryCollision: collision,
            MergeState: WorkerMergeStateNames.ToApiString(mergeState),
            MergeReadiness: readiness,
            MergeConflict: conflict,
            DecisionSummary: null!,
            ApproveGuardrails: null!,
            RevokeGuardrails: null!,
            RecommendedModeSlug: string.Empty,
            AvailableModeTransitions: Array.Empty<ModeTransitionHint>()));
    }

    private ParallelWorkerMirrorEntry EnrichWorkerEntry(ParallelWorkerMirrorEntry entry)
    {
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
        return withDecision with
        {
            RecommendedModeSlug = modeHints.RecommendedModeSlug,
            AvailableModeTransitions = modeHints.AvailableTransitions,
        };
    }

    private static async Task<IReadOnlyList<string>> ResolveChangedPathsForLeaseAsync(
        ExecutionLease lease,
        string sessionRoot,
        CancellationToken cancellationToken)
    {
        if (lease.Status != ExecutionLeaseStatus.ReadyForReview)
            return Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(lease.VerificationReportJson))
        {
            try
            {
                var report = VerificationReportSerializer.Deserialize(lease.VerificationReportJson);
                if (report.ChangedFiles.Count > 0)
                    return report.ChangedFiles.ToList();
            }
            catch
            {
                // fall through to git
            }
        }

        if (string.IsNullOrWhiteSpace(lease.WorktreePath) || !Directory.Exists(lease.WorktreePath))
            return Array.Empty<string>();

        var git = await GitWorkspaceStatus.TryGetWorktreeSummaryAsync(
            lease.WorktreePath,
            sessionRoot,
            cancellationToken);
        return git?.ChangedPaths.ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    private static LiveMirrorHealthState ResolveHealth(
        WorkspaceLiveMirrorRegistry.MirrorObservabilityOutcome? outcome,
        MirrorMetaOnDisk? meta,
        ExecutionLease lease,
        bool isSharedSessionRoot,
        int activeLeasesInWorkspace)
    {
        if (outcome is not null
            && outcome.Health is not LiveMirrorHealthState.Active and not LiveMirrorHealthState.Unknown)
            return outcome.Health;

        if (meta is not null)
            return LiveMirrorHealthStateNames.Parse(meta.LifecycleStatus);

        if (KanbanExecutionRules.IsActiveLease(lease)
            && isSharedSessionRoot
            && activeLeasesInWorkspace > 1)
            return LiveMirrorHealthState.SkippedParallelSharedRootGuard;

        if (KanbanExecutionRules.IsActiveLease(lease))
            return LiveMirrorHealthState.Unknown;

        return LiveMirrorHealthState.Unknown;
    }

    private void MergeDiskMirrors(
        string sessionRoot,
        List<ParallelWorkerMirrorEntry> workers,
        List<MirrorObservabilityWarning> warnings)
    {
        var liveBase = Path.Combine(sessionRoot, WorkspaceLiveMirrorPaths.LiveRootSegment);
        if (!Directory.Exists(liveBase))
            return;

        var knownRoots = new HashSet<string>(
            workers.Select(w => w.LiveMirrorPath).Where(p => !string.IsNullOrEmpty(p))!,
            StringComparer.OrdinalIgnoreCase);

        foreach (var taskDir in Directory.EnumerateDirectories(liveBase))
        {
            foreach (var mirrorDir in EnumerateMirrorDirs(taskDir))
            {
                if (knownRoots.Contains(mirrorDir))
                    continue;

                var meta = ReadMirrorMeta(mirrorDir);
                if (meta is null)
                    continue;

                if (!Guid.TryParse(meta.TaskId, out var taskId))
                    continue;

                var mergeState = meta.MergeState ?? WorkerMergeStateNames.ToApiString(WorkerMergeState.Stale);
                workers.Add(EnrichWorkerEntry(new ParallelWorkerMirrorEntry(
                    TaskId: taskId,
                    TaskTitle: meta.TaskTitle ?? taskId.ToString(),
                    ExecutionSessionId: Guid.TryParse(meta.ExecutionSessionId, out var eid) ? eid : null,
                    LeaseId: Guid.TryParse(meta.LeaseId, out var lid) ? lid : Guid.Empty,
                    HermesSessionId: meta.HermesSessionId,
                    LiveMirrorPath: mirrorDir,
                    LiveMarkdownPath: Path.Combine(mirrorDir, _workspace.LiveStatusFileName),
                    HealthState: LiveMirrorHealthStateNames.ToApiString(
                        LiveMirrorHealthStateNames.Parse(meta.LifecycleStatus)),
                    LifecycleStatus: meta.LifecycleStatus,
                    LastMirroredAt: meta.LastMirroredAt,
                    KanbanRevision: meta.KanbanRevision,
                    KanbanPushedRevision: meta.KanbanPushedRevision,
                    KanbanStatus: meta.KanbanStatus ?? "Unknown",
                    LeaseStatus: meta.LeaseStatus ?? "Unknown",
                    WorktreePath: meta.WorktreePath,
                    IsSharedSessionRootMirror: false,
                    RegistryCollision: null,
                    MergeState: mergeState,
                    MergeReadiness: null,
                    MergeConflict: null,
                    DecisionSummary: null!,
                    ApproveGuardrails: null!,
                    RevokeGuardrails: null!,
                    RecommendedModeSlug: string.Empty,
                    AvailableModeTransitions: Array.Empty<ModeTransitionHint>())));

                if (meta.LifecycleStatus.Equals("pruned", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(new MirrorObservabilityWarning(
                        "pruned",
                        $"Mirror at {mirrorDir} was pruned.",
                        Guid.TryParse(meta.LeaseId, out var plid) ? plid : null,
                        taskId));
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateMirrorDirs(string taskDir)
    {
        if (File.Exists(Path.Combine(taskDir, ".joyzoning", "mirror-meta.json")))
        {
            yield return taskDir;
            yield break;
        }

        foreach (var sub in Directory.EnumerateDirectories(taskDir))
            yield return sub;
    }

    private static MirrorMetaOnDisk? ReadMirrorMeta(string mirrorRoot)
    {
        var path = Path.Combine(mirrorRoot, ".joyzoning", "mirror-meta.json");
        if (!File.Exists(path))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            return new MirrorMetaOnDisk(
                LifecycleStatus: root.TryGetProperty("status", out var s) ? s.GetString() ?? "active" : "active",
                MergeState: root.TryGetProperty("mergeState", out var ms) ? ms.GetString() : null,
                LastMirroredAt: root.TryGetProperty("lastMirroredAt", out var lm)
                    && DateTimeOffset.TryParse(lm.GetString(), out var mirrored)
                    ? mirrored
                    : root.TryGetProperty("updatedAt", out var u)
                        && DateTimeOffset.TryParse(u.GetString(), out var updated)
                        ? updated
                        : null,
                TaskId: root.TryGetProperty("taskId", out var ti) ? ti.GetString() : null,
                TaskTitle: root.TryGetProperty("taskTitle", out var tt) ? tt.GetString() : null,
                LeaseId: root.TryGetProperty("leaseId", out var li) ? li.GetString() : null,
                ExecutionSessionId: root.TryGetProperty("executionSessionId", out var ei) ? ei.GetString() : null,
                HermesSessionId: root.TryGetProperty("hermesSessionId", out var hi) ? hi.GetString() : null,
                KanbanRevision: root.TryGetProperty("kanbanRevision", out var kr) ? kr.GetInt64() : 0,
                KanbanPushedRevision: root.TryGetProperty("kanbanPushedRevision", out var kp) ? kp.GetInt64() : 0,
                KanbanStatus: root.TryGetProperty("kanbanStatus", out var ks) ? ks.GetString() : null,
                LeaseStatus: root.TryGetProperty("leaseStatus", out var ls) ? ls.GetString() : null,
                WorktreePath: root.TryGetProperty("worktreePath", out var wp) ? wp.GetString() : null);
        }
        catch
        {
            return null;
        }
    }

    private sealed record MirrorMetaOnDisk(
        string LifecycleStatus,
        string? MergeState,
        DateTimeOffset? LastMirroredAt,
        string? TaskId,
        string? TaskTitle,
        string? LeaseId,
        string? ExecutionSessionId,
        string? HermesSessionId,
        long KanbanRevision,
        long KanbanPushedRevision,
        string? KanbanStatus,
        string? LeaseStatus,
        string? WorktreePath);
}
