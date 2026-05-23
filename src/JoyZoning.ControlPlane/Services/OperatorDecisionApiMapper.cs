using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

internal static class OperatorDecisionApiMapper
{
    public static object MapSummary(OperatorDecisionSummary s) => new
    {
        taskId = s.TaskId,
        taskTitle = s.TaskTitle,
        executionSessionId = s.ExecutionSessionId,
        leaseId = s.LeaseId,
        mergeState = s.MergeState,
        changedFilesCount = s.ChangedFilesCount,
        changedFilesSummary = s.ChangedFilesSummary,
        verificationStatus = s.VerificationStatus,
        conflictStatus = s.ConflictStatus,
        baseCommit = s.BaseCommit,
        headCommit = s.HeadCommit,
        worktreePath = s.WorktreePath,
        liveMirrorPath = s.LiveMirrorPath,
        riskFlags = MapRiskFlags(s.RiskFlags),
    };

    public static object MapGuardrails(OperatorActionGuardrails g) => new
    {
        action = g.Action,
        blocked = g.Blocked,
        requiresAcknowledgement = g.RequiresAcknowledgement,
        warnings = g.Warnings,
        blockReasons = g.BlockReasons,
    };

    public static object MapPreflight(OperatorDecisionPreflight p) => new
    {
        sessionId = p.SessionId,
        executionSessionId = p.ExecutionSessionId,
        taskId = p.TaskId,
        action = p.Action,
        decisionSummary = MapSummary(p.DecisionSummary),
        guardrails = MapGuardrails(p.Guardrails),
    };

    public static object MapWorkerDecisionFields(ParallelWorkerMirrorEntry w) => new
    {
        decisionSummary = MapSummary(w.DecisionSummary),
        approveGuardrails = MapGuardrails(w.ApproveGuardrails),
        revokeGuardrails = MapGuardrails(w.RevokeGuardrails),
    };

    private static object MapRiskFlags(OperatorRiskFlags f) => new
    {
        hasConflicts = f.HasConflicts,
        verificationFailed = f.VerificationFailed,
        verificationMissing = f.VerificationMissing,
        dirtyWorktree = f.DirtyWorktree,
        overlapsWithOtherReadyWorker = f.OverlapsWithOtherReadyWorker,
        largeChangeSet = f.LargeChangeSet,
        staleWorker = f.StaleWorker,
        mirrorMissing = f.MirrorMissing,
        unknownHeadCommit = f.UnknownHeadCommit,
        active = f.ActiveFlagNames(),
    };
}
