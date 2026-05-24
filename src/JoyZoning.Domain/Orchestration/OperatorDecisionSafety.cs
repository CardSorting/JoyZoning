using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Read-model guardrails for human approve/revoke/inspect decisions on workers.</summary>
public static class OperatorDecisionSafety
{
    public const int DefaultLargeChangeSetThreshold = 20;

    public static (OperatorDecisionSummary Summary, OperatorActionGuardrails Approve, OperatorActionGuardrails Revoke)
        BuildForWorker(
            ParallelWorkerEntry worker,
            int largeChangeSetThreshold = DefaultLargeChangeSetThreshold)
    {
        var summary = BuildSummary(worker, largeChangeSetThreshold);
        var approve = BuildApproveGuardrails(summary);
        var revoke = BuildRevokeGuardrails(summary);
        return (summary, approve, revoke);
    }

    public static OperatorDecisionPreflight BuildPreflight(
        Guid sessionId,
        string action,
        ParallelWorkerEntry worker,
        int largeChangeSetThreshold = DefaultLargeChangeSetThreshold)
    {
        var (summary, approve, revoke) = BuildForWorker(worker, largeChangeSetThreshold);
        var guardrails = action switch
        {
            OperatorDecisionActions.Accept => approve,
            OperatorDecisionActions.Revoke => revoke,
            OperatorDecisionActions.Inspect => BuildInspectGuardrails(summary),
            _ => approve,
        };

        return new OperatorDecisionPreflight(
            sessionId,
            worker.ExecutionSessionId,
            worker.TaskId,
            action,
            summary,
            guardrails);
    }

    public static OperatorDecisionSummary BuildSummary(
        ParallelWorkerEntry worker,
        int largeChangeSetThreshold = DefaultLargeChangeSetThreshold)
    {
        var r = worker.MergeReadiness;
        var c = worker.MergeConflict;
        var changedCount = r?.ChangedFilesCount ?? 0;
        var summaryPaths = r?.ChangedFilesSummary ?? Array.Empty<string>();

        var verificationStatus = ResolveVerificationStatus(worker, r);
        var conflictStatus = ResolveConflictStatus(worker, c);

        var flags = new OperatorRiskFlags(
            HasConflicts: HasConflicts(worker, c),
            VerificationFailed: verificationStatus == "failed",
            VerificationMissing: verificationStatus == "missing",
            DirtyWorktree: r?.IsDirty == true,
            OverlapsWithOtherReadyWorker: c?.Category == "overlapping_files",
            LargeChangeSet: changedCount >= largeChangeSetThreshold,
            StaleWorker: worker.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.Stale),
            UnknownHeadCommit: !string.IsNullOrWhiteSpace(r?.WorktreePath)
                && string.IsNullOrWhiteSpace(r.HeadCommit));

        return new OperatorDecisionSummary(
            TaskId: worker.TaskId,
            TaskTitle: worker.TaskTitle,
            ExecutionSessionId: worker.ExecutionSessionId,
            LeaseId: worker.LeaseId,
            MergeState: worker.MergeState,
            ChangedFilesCount: changedCount,
            ChangedFilesSummary: summaryPaths,
            VerificationStatus: verificationStatus,
            ConflictStatus: conflictStatus,
            BaseCommit: r?.BaseCommit,
            HeadCommit: r?.HeadCommit,
            WorktreePath: worker.WorkspacePath ?? r?.WorktreePath,
            RiskFlags: flags);
    }

    public static OperatorActionGuardrails BuildApproveGuardrails(OperatorDecisionSummary summary)
    {
        var blocks = new List<string>();
        var warnings = new List<string>();

        if (summary.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.MergeConflict))
            blocks.Add("Worker is in merge_conflict — resolve conflicts before approving merge.");

        if (summary.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.MergeFailed))
            blocks.Add("Worker is in merge_failed — verification or merge preconditions failed.");

        if (summary.RiskFlags.HasConflicts && blocks.Count == 0)
            blocks.Add("Unresolved merge conflicts detected in the workspace.");

        if (summary.RiskFlags.VerificationMissing)
            warnings.Add("No passing verification report on this lease.");

        if (summary.RiskFlags.VerificationFailed)
            warnings.Add("Verification did not pass — merge is unsafe without a new passing run.");

        if (summary.RiskFlags.OverlapsWithOtherReadyWorker)
            warnings.Add("Changed files overlap another worker that is also ready to merge.");

        if (summary.RiskFlags.DirtyWorktree)
            warnings.Add("Workspace has uncommitted or unstaged changes.");

        if (summary.RiskFlags.LargeChangeSet)
            warnings.Add($"Large change set ({summary.ChangedFilesCount} files) — review carefully before merge.");

        if (summary.RiskFlags.StaleWorker)
            warnings.Add("Worker is stale — confirm the run is still current.");

        if (summary.RiskFlags.UnknownHeadCommit)
            warnings.Add("Head commit could not be resolved from the workspace.");

        var blocked = blocks.Count > 0;
        return new OperatorActionGuardrails(
            OperatorDecisionActions.Accept,
            Blocked: blocked,
            RequiresAcknowledgement: !blocked && warnings.Count > 0,
            Warnings: warnings,
            BlockReasons: blocks);
    }

    public static OperatorActionGuardrails BuildRevokeGuardrails(OperatorDecisionSummary summary)
    {
        var warnings = new List<string>();

        if (summary.ChangedFilesCount > 0)
        {
            warnings.Add(
                $"Workspace has {summary.ChangedFilesCount} changed file(s). Revoke preserves the canonical workspace for inspection.");
        }

        if (summary.RiskFlags.HasConflicts)
            warnings.Add("Conflicts are present — revoking does not revert committed changes.");

        if (summary.RiskFlags.VerificationFailed)
            warnings.Add("Verification failed on this worker — revoke if abandoning the run.");

        if (string.IsNullOrWhiteSpace(summary.WorktreePath))
            warnings.Add("No workspace path recorded — evidence may be limited.");

        return new OperatorActionGuardrails(
            OperatorDecisionActions.Revoke,
            Blocked: false,
            RequiresAcknowledgement: warnings.Count > 0,
            Warnings: warnings,
            BlockReasons: Array.Empty<string>());
    }

    public static OperatorActionGuardrails BuildInspectGuardrails(OperatorDecisionSummary summary)
    {
        var warnings = new List<string>();
        if (summary.RiskFlags.HasConflicts)
            warnings.Add("Inspect conflict files in the workspace before resolving.");

        return new OperatorActionGuardrails(
            OperatorDecisionActions.Inspect,
            Blocked: false,
            RequiresAcknowledgement: false,
            Warnings: warnings,
            BlockReasons: Array.Empty<string>());
    }

    private static bool HasConflicts(ParallelWorkerEntry worker, MergeConflictDetail? c) =>
        worker.MergeState is "merge_conflict" or "merge_failed" || c is not null;

    private static string ResolveVerificationStatus(
        ParallelWorkerEntry worker,
        WorkerMergeReadiness? r)
    {
        if (worker.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.MergeFailed))
            return "failed";

        if (r?.VerificationPassed == true)
            return "passed";

        if (r?.VerificationPassed == false)
            return "failed";

        if (r?.TestsRun == true && r.VerificationPassed is null)
            return "unknown";

        if (worker.LeaseStatus == ExecutionLeaseStatus.ReadyForReview.ToString()
            || worker.MergeState == WorkerMergeStateNames.ToApiString(WorkerMergeState.ReadyToMerge))
            return "missing";

        return "not_run";
    }

    private static string ResolveConflictStatus(ParallelWorkerEntry worker, MergeConflictDetail? c)
    {
        if (c is not null)
            return $"{c.Category}: {c.Reason}";

        if (worker.MergeState is "merge_conflict" or "merge_failed")
            return worker.MergeState;

        return "none";
    }
}
