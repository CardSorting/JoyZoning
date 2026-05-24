using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Pre-autopilot merge observability: overlaps, conflicts, and changed-file resolution.</summary>
public sealed class AuthorityAutopilotMergeContextBuilder
{
    private readonly IWorkTaskRepository _tasks;
    private readonly IExecutionLeaseRepository _leases;
    private readonly IExecutionRepository _executions;
    private readonly WorkerMergeObservabilityBuilder _mergeBuilder;

    public AuthorityAutopilotMergeContextBuilder(
        IWorkTaskRepository tasks,
        IExecutionLeaseRepository leases,
        IExecutionRepository executions,
        WorkerMergeObservabilityBuilder mergeBuilder)
    {
        _tasks = tasks;
        _leases = leases;
        _executions = executions;
        _mergeBuilder = mergeBuilder;
    }

    public async Task<AutopilotMergeSnapshot> BuildForLeaseAsync(
        ExecutionLease lease,
        OperatorSession session,
        CancellationToken cancellationToken = default)
    {
        var sessionRoot = session.WorkspaceRoot;
        var readyLeases = await ListReadyForReviewInSessionAsync(session.Id, cancellationToken);
        var readyPathData = new List<(ExecutionLease Lease, IReadOnlyList<string> Paths)>();

        var pathResolutionFailed = false;
        foreach (var ready in readyLeases)
        {
            var paths = await WorkerMergeObservabilityBuilder.ResolveChangedPathsForLeaseAsync(
                ready,
                sessionRoot,
                cancellationToken);
            if (paths.Count == 0)
            {
                var hasVerificationPaths = TryReadVerificationPaths(ready);
                if (!hasVerificationPaths)
                {
                    if (string.IsNullOrWhiteSpace(ready.WorktreePath)
                        || !Directory.Exists(ready.WorktreePath))
                    {
                        pathResolutionFailed = true;
                    }
                    else if (readyLeases.Count > 1)
                    {
                        pathResolutionFailed = true;
                    }
                }
            }

            readyPathData.Add((ready, paths));
        }

        var readyFileMap = WorkerMergeObservabilityBuilder.BuildReadyWorkerFileMap(readyPathData);
        var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken)
            ?? throw new InvalidOperationException("Task not found for lease.");

        ExecutionPhase? executionPhase = null;
        if (lease.ExecutionSessionId is { } execId && execId != Guid.Empty)
        {
            var execution = await _executions.GetByIdAsync(execId, cancellationToken);
            executionPhase = execution?.Phase;
        }

        var (mergeState, readiness, conflict) = await _mergeBuilder.EnrichAsync(
            lease,
            task,
            sessionRoot,
            executionPhase,
            readyFileMap,
            cancellationToken);

        var mergeStateApi = WorkerMergeStateNames.ToApiString(mergeState);
        var overlapping = conflict?.Category == "overlapping_files"
            || (readyFileMap.TryGetValue(lease.Id, out var mine)
                && readyLeases.Count > 1
                && readyFileMap.Any(kv => kv.Key != lease.Id && mine.Overlaps(kv.Value)));

        var overlapPaths = conflict?.ConflictFiles.Count > 0
            ? conflict.ConflictFiles
            : overlapping && readyFileMap.TryGetValue(lease.Id, out var selfFiles)
                ? selfFiles
                    .Intersect(
                        readyFileMap.Where(kv => kv.Key != lease.Id).SelectMany(kv => kv.Value),
                        StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToList()
                : [];

        var leasePaths = readyPathData.FirstOrDefault(r => r.Lease.Id == lease.Id).Paths
            ?? Array.Empty<string>();
        var changedFiles = readiness?.ChangedFilesSummary?.Count > 0
            ? readiness.ChangedFilesSummary.ToList()
            : leasePaths.ToList();

        var readyToMergeApi = WorkerMergeStateNames.ToApiString(WorkerMergeState.ReadyToMerge);
        var mergeObservabilityUnknown = pathResolutionFailed
            || (readyLeases.Count > 1 && changedFiles.Count == 0 && mergeStateApi == readyToMergeApi);

        return new AutopilotMergeSnapshot(
            mergeStateApi,
            readiness,
            conflict,
            overlapping,
            mergeObservabilityUnknown,
            overlapPaths);
    }

    private async Task<List<ExecutionLease>> ListReadyForReviewInSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var active = await _leases.ListActiveAsync(cancellationToken);
        var ready = new List<ExecutionLease>();
        foreach (var lease in active)
        {
            if (lease.Status != ExecutionLeaseStatus.ReadyForReview)
                continue;

            var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
            if (task?.OperatorSessionId == sessionId)
                ready.Add(lease);
        }

        return ready;
    }

    private static bool TryReadVerificationPaths(ExecutionLease lease)
    {
        if (string.IsNullOrWhiteSpace(lease.VerificationReportJson))
            return false;

        try
        {
            var report = VerificationReportSerializer.Deserialize(lease.VerificationReportJson);
            return report.ChangedFiles.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public static AutopilotMergeSnapshot FromWorkerEntry(
        string mergeState,
        WorkerMergeReadiness? readiness,
        MergeConflictDetail? conflict,
        int readyForReviewPeerCount)
    {
        var overlapping = conflict?.Category == "overlapping_files";
        var overlapPaths = conflict?.ConflictFiles ?? Array.Empty<string>();
        var readyToMerge = WorkerMergeStateNames.ToApiString(WorkerMergeState.ReadyToMerge);
        var mergeObservabilityUnknown = readyForReviewPeerCount > 1
            && (readiness?.ChangedFilesCount ?? 0) == 0
            && mergeState == readyToMerge
            && !overlapping;

        return new AutopilotMergeSnapshot(
            mergeState,
            readiness,
            conflict,
            overlapping,
            mergeObservabilityUnknown,
            overlapPaths);
    }
}

public sealed record AutopilotMergeSnapshot(
    string MergeState,
    WorkerMergeReadiness? Readiness,
    MergeConflictDetail? Conflict,
    bool OverlappingReadyWorker,
    bool MergeObservabilityUnknown,
    IReadOnlyList<string> OverlappingPaths);
