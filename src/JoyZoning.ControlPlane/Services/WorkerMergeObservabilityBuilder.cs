using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

public sealed class WorkerMergeObservabilityBuilder
{
    public async Task<(WorkerMergeState MergeState, WorkerMergeReadiness? Readiness, MergeConflictDetail? Conflict)>
        EnrichAsync(
            ExecutionLease lease,
            WorkTask task,
            string sessionWorkspaceRoot,
            string? liveMirrorPath,
            ExecutionPhase? executionPhase,
            string? mirrorLifecycleStatus,
            string? persistedMergeState,
            IReadOnlyDictionary<Guid, HashSet<string>> readyWorkerFileMap,
            CancellationToken cancellationToken)
    {
        VerificationReport? passing = TryDeserializeReport(lease.VerificationReportJson);
        VerificationReport? failed = TryDeserializeReport(lease.FailedVerificationReportJson);

        GitWorktreeSummary? git = null;
        if (!string.IsNullOrWhiteSpace(lease.WorktreePath) && Directory.Exists(lease.WorktreePath))
        {
            git = await GitWorkspaceStatus.TryGetWorktreeSummaryAsync(
                lease.WorktreePath,
                sessionWorkspaceRoot,
                cancellationToken);
        }

        var changedPaths = git?.ChangedPaths ?? passing?.ChangedFiles ?? Array.Empty<string>();
        var summaryPaths = changedPaths.Take(8).ToList();

        var overlapping = false;
        HashSet<string>? readyFiles = null;
        if (lease.Status == ExecutionLeaseStatus.ReadyForReview
            && readyWorkerFileMap.TryGetValue(lease.Id, out readyFiles))
        {
            foreach (var (otherLeaseId, otherFiles) in readyWorkerFileMap)
            {
                if (otherLeaseId == lease.Id)
                    continue;

                if (readyFiles.Overlaps(otherFiles))
                {
                    overlapping = true;
                    break;
                }
            }
        }

        var gitConflicts = git?.ConflictPaths ?? Array.Empty<string>();
        var (state, conflict) = WorkerMergeStateResolver.Resolve(
            lease,
            task,
            executionPhase,
            mirrorLifecycleStatus,
            persistedMergeState,
            passing,
            failed,
            overlapping,
            gitConflicts.ToList());

        if (overlapping && readyFiles is not null)
        {
            var overlapFiles = readyFiles
                .Intersect(
                    readyWorkerFileMap
                        .Where(kv => kv.Key != lease.Id)
                        .SelectMany(kv => kv.Value),
                    StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            if (conflict is { Category: "overlapping_files" } && conflict.ConflictFiles.Count == 0 && overlapFiles.Count > 0)
            {
                conflict = conflict with { ConflictFiles = overlapFiles };
            }
            else if (conflict is null && overlapFiles.Count > 0)
            {
                conflict = new MergeConflictDetail(
                    "overlapping_files",
                    "Changed files overlap another ready worker.",
                    overlapFiles);
            }
        }

        if (state == WorkerMergeState.MergeConflict && conflict is not null && conflict.ConflictFiles.Count == 0 && gitConflicts.Count > 0)
        {
            conflict = conflict with { ConflictFiles = gitConflicts };
        }

        var testsRun = passing?.CommandsRun.Count > 0 || failed?.CommandsRun.Count > 0;
        var verificationPassed = passing is not null && VerificationReportValidator.IsPassingReport(passing);
        var verificationSummary = passing is not null
            ? $"{passing.CommandsRun.Count(cmd => cmd.Passed)}/{passing.CommandsRun.Count} commands passed"
            : failed is not null
                ? $"{failed.CommandsRun.Count(cmd => !cmd.Passed)} command(s) failed"
                : null;

        var gitConvergence = GitConvergenceEvidence.TryReadLastSummary(lease.EvidenceLogJson);

        var readiness = new WorkerMergeReadiness(
            ExecutionSessionId: lease.ExecutionSessionId,
            WorktreePath: lease.WorktreePath,
            LiveMirrorPath: liveMirrorPath,
            MergeTargetWorkspaceRoot: sessionWorkspaceRoot,
            MergeTargetBranch: git?.BranchName ?? lease.BranchName,
            HeadCommit: git?.HeadCommit,
            BaseCommit: git?.BaseCommit,
            IsDirty: git?.IsDirty ?? changedPaths.Count > 0,
            ChangedFilesCount: changedPaths.Count,
            ChangedFilesSummary: summaryPaths,
            VerificationPassed: testsRun == true ? verificationPassed : null,
            TestsRun: testsRun,
            VerificationSummary: verificationSummary,
            GitConvergence: gitConvergence);

        return (state, readiness, conflict);
    }

    public static IReadOnlyDictionary<Guid, HashSet<string>> BuildReadyWorkerFileMap(
        IEnumerable<(ExecutionLease Lease, IReadOnlyList<string> Paths)> readyLeases)
    {
        var map = new Dictionary<Guid, HashSet<string>>();
        foreach (var (lease, paths) in readyLeases)
            map[lease.Id] = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return map;
    }

    public static MergeQueueResponse BuildQueue(
        Guid sessionId,
        string sessionRoot,
        IReadOnlyList<ParallelWorkerMirrorEntry> workers,
        IReadOnlyList<MirrorObservabilityWarning> warnings)
    {
        var ready = workers
            .Where(w => w.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.ReadyToMerge))
            .ToList();
        var conflicts = workers
            .Where(w => w.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.MergeConflict)
                        || w.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.MergeFailed))
            .ToList();
        var completed = workers
            .Where(w => w.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.Merged))
            .ToList();
        var revokedAbandoned = workers
            .Where(w =>
                w.MergeState is "revoked" or "abandoned"
                || w.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.Stale))
            .ToList();

        return new MergeQueueResponse(
            sessionId,
            sessionRoot,
            DateTimeOffset.UtcNow,
            ready,
            conflicts,
            completed,
            revokedAbandoned,
            workers,
            warnings);
    }

    private static VerificationReport? TryDeserializeReport(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return VerificationReportSerializer.Deserialize(json);
        }
        catch
        {
            return null;
        }
    }
}
