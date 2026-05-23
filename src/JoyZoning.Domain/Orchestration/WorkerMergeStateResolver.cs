using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public static class WorkerMergeStateResolver
{
    public static (WorkerMergeState State, MergeConflictDetail? Conflict) Resolve(
        ExecutionLease lease,
        WorkTask task,
        ExecutionPhase? executionPhase,
        string? mirrorLifecycleStatus,
        string? persistedMergeState,
        VerificationReport? passingReport,
        VerificationReport? failedReport,
        bool hasOverlappingFilesWithOtherReadyWorkers,
        IReadOnlyList<string> gitConflictPaths)
    {
        if (!string.IsNullOrWhiteSpace(persistedMergeState))
        {
            var persisted = WorkerMergeStateNames.Parse(persistedMergeState);
            if (persisted is WorkerMergeState.MergeConflict or WorkerMergeState.Merged or WorkerMergeState.Revoked)
                return (persisted, null);
        }

        if (lease.Status == ExecutionLeaseStatus.Merged)
            return (WorkerMergeState.Merged, null);

        if (lease.Status == ExecutionLeaseStatus.Revoked)
            return (WorkerMergeState.Revoked, null);

        if (string.Equals(mirrorLifecycleStatus, "stale", StringComparison.OrdinalIgnoreCase)
            && lease.Status is ExecutionLeaseStatus.Verifying or ExecutionLeaseStatus.Blocked)
            return (WorkerMergeState.Stale, null);

        if (executionPhase is ExecutionPhase.Failed or ExecutionPhase.Cancelled or ExecutionPhase.Interrupted
            && !KanbanExecutionRules.IsTerminal(lease.Status))
        {
            return (WorkerMergeState.Abandoned, new MergeConflictDetail(
                "execution_ended",
                $"Execution phase is {executionPhase}.",
                Array.Empty<string>()));
        }

        if (gitConflictPaths.Count > 0)
        {
            return (WorkerMergeState.MergeConflict, new MergeConflictDetail(
                "unmerged_paths",
                "Worktree has unmerged git paths.",
                gitConflictPaths));
        }

        if (hasOverlappingFilesWithOtherReadyWorkers)
        {
            return (WorkerMergeState.MergeConflict, new MergeConflictDetail(
                "overlapping_files",
                "Changed files overlap another worker that is also ready to merge.",
                Array.Empty<string>()));
        }

        if (failedReport is not null && !failedReport.AllCommandsPassed)
        {
            return (WorkerMergeState.MergeFailed, new MergeConflictDetail(
                "verification_failed",
                "Verification commands did not all pass.",
                failedReport.ChangedFiles.ToList()));
        }

        if (lease.Status == ExecutionLeaseStatus.ReadyForReview)
        {
            var mergeError = KanbanExecutionRules.ValidateHumanMerge(lease);
            if (mergeError is not null)
            {
                return (WorkerMergeState.MergeConflict, new MergeConflictDetail(
                    "merge_precondition",
                    mergeError,
                    Array.Empty<string>()));
            }

            return (WorkerMergeState.ReadyToMerge, null);
        }

        if (lease.Status == ExecutionLeaseStatus.Running)
            return (WorkerMergeState.Running, null);

        if (lease.Status is ExecutionLeaseStatus.Verifying or ExecutionLeaseStatus.Blocked)
            return (WorkerMergeState.Stale, null);

        return (WorkerMergeState.Running, null);
    }

    public static bool IsTerminalMergeBucket(WorkerMergeState state) =>
        state is WorkerMergeState.Merged or WorkerMergeState.Revoked or WorkerMergeState.Abandoned;

    public static bool BlocksPrune(WorkerMergeState state) =>
        state is WorkerMergeState.MergeConflict or WorkerMergeState.MergeFailed;
}
