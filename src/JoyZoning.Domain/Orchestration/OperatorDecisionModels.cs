namespace JoyZoning.Domain.Orchestration;

public sealed record OperatorRiskFlags(
    bool HasConflicts,
    bool VerificationFailed,
    bool VerificationMissing,
    bool DirtyWorktree,
    bool OverlapsWithOtherReadyWorker,
    bool LargeChangeSet,
    bool StaleWorker,
    bool MirrorMissing,
    bool UnknownHeadCommit)
{
    public IReadOnlyList<string> ActiveFlagNames()
    {
        var names = new List<string>();
        if (HasConflicts) names.Add("has_conflicts");
        if (VerificationFailed) names.Add("verification_failed");
        if (VerificationMissing) names.Add("verification_missing");
        if (DirtyWorktree) names.Add("dirty_worktree");
        if (OverlapsWithOtherReadyWorker) names.Add("overlaps_with_other_ready_worker");
        if (LargeChangeSet) names.Add("large_change_set");
        if (StaleWorker) names.Add("stale_worker");
        if (MirrorMissing) names.Add("mirror_missing");
        if (UnknownHeadCommit) names.Add("unknown_head_commit");
        return names;
    }
}

public sealed record OperatorDecisionSummary(
    Guid TaskId,
    string TaskTitle,
    Guid? ExecutionSessionId,
    Guid LeaseId,
    string MergeState,
    int ChangedFilesCount,
    IReadOnlyList<string> ChangedFilesSummary,
    string VerificationStatus,
    string ConflictStatus,
    string? BaseCommit,
    string? HeadCommit,
    string? WorktreePath,
    string? LiveMirrorPath,
    OperatorRiskFlags RiskFlags);

public sealed record OperatorActionGuardrails(
    string Action,
    bool Blocked,
    bool RequiresAcknowledgement,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> BlockReasons);

public sealed record OperatorDecisionPreflight(
    Guid SessionId,
    Guid? ExecutionSessionId,
    Guid TaskId,
    string Action,
    OperatorDecisionSummary DecisionSummary,
    OperatorActionGuardrails Guardrails);

public static class OperatorDecisionActions
{
    /// <summary>Canonical preflight action for accept-result (git convergence + Merged).</summary>
    public const string Accept = "accept";

    /// <summary>Legacy alias for <see cref="Accept"/>.</summary>
    public const string Approve = "approve";

    public const string Revoke = "revoke";
    public const string Inspect = "inspect";

    public static bool TryParse(string? value, out string action)
    {
        var raw = (value ?? string.Empty).Trim().ToLowerInvariant();
        action = raw switch
        {
            Approve => Accept,
            Accept => Accept,
            Revoke => Revoke,
            Inspect => Inspect,
            _ => raw,
        };

        return action is Accept or Revoke or Inspect;
    }
}
