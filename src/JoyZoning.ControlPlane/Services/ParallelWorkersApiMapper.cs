using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

internal static class ParallelWorkersApiMapper
{
    public static object ToJson(ParallelWorkersResponse model) => new
    {
        sessionId = model.SessionId,
        sessionWorkspaceRoot = model.SessionWorkspaceRoot,
        updatedAt = model.UpdatedAt,
        parallelActive = model.ParallelActive,
        protocol = model.Protocol,
        authority = new
        {
            profile = model.Authority.Profile,
            profileLabel = model.Authority.ProfileLabel,
            autopilotEnabled = model.Authority.AutopilotEnabled,
        },
        warnings = model.Warnings.Select(w => new
        {
            w.Code,
            w.Message,
            w.LeaseId,
            w.TaskId,
        }),
        workers = model.Workers.Select(MapWorker),
    };

    public static object MapWorker(ParallelWorkerEntry w) => new
    {
        taskId = w.TaskId,
        taskTitle = w.TaskTitle,
        executionSessionId = w.ExecutionSessionId,
        leaseId = w.LeaseId,
        hermesSessionId = w.HermesSessionId,
        workspacePath = w.WorkspacePath,
        kanbanRevision = w.KanbanRevision,
        kanbanPushedRevision = w.KanbanPushedRevision,
        kanbanStatus = w.KanbanStatus,
        leaseStatus = w.LeaseStatus,
        mergeState = w.MergeState,
        mergeReadiness = MapReadiness(w.MergeReadiness),
        mergeConflict = MapConflict(w.MergeConflict),
        decisionSummary = OperatorDecisionApiMapper.MapSummary(w.DecisionSummary),
        approveGuardrails = OperatorDecisionApiMapper.MapGuardrails(w.ApproveGuardrails),
        revokeGuardrails = OperatorDecisionApiMapper.MapGuardrails(w.RevokeGuardrails),
        recommendedMode = w.RecommendedModeSlug,
        availableModeTransitions = w.AvailableModeTransitions.Select(t => new
        {
            targetMode = t.TargetModeSlug,
            t.Label,
            t.Reason,
            handoffKind = t.HandoffKind,
        }),
        authorityProfile = w.AuthorityProfileSlug,
        authority = w.Authority is null
            ? null
            : new
            {
                profile = w.Authority.Profile.ToString(),
                w.Authority.RiskLevel,
                w.Authority.AutoAcceptAllowed,
                w.Authority.NeedsHumanReview,
                reasonCodes = w.Authority.ReasonCodes,
                humanMessages = w.Authority.HumanMessages,
            },
    };

    private static object? MapReadiness(WorkerMergeReadiness? r) =>
        r is null
            ? null
            : new
            {
                executionSessionId = r.ExecutionSessionId,
                worktreePath = r.WorktreePath,
                mergeTargetWorkspaceRoot = r.MergeTargetWorkspaceRoot,
                mergeTargetBranch = r.MergeTargetBranch,
                headCommit = r.HeadCommit,
                baseCommit = r.BaseCommit,
                isDirty = r.IsDirty,
                changedFilesCount = r.ChangedFilesCount,
                changedFilesSummary = r.ChangedFilesSummary,
                verificationPassed = r.VerificationPassed,
                testsRun = r.TestsRun,
                verificationSummary = r.VerificationSummary,
                gitConvergence = r.GitConvergence,
            };

    private static object? MapConflict(MergeConflictDetail? c) =>
        c is null
            ? null
            : new
            {
                c.Category,
                c.Reason,
                conflictFiles = c.ConflictFiles,
            };
}
