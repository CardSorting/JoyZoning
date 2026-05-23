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
        liveMirrorMode = model.LiveMirrorMode,
        disableSharedSessionRootMirrorWhenParallel = model.DisableSharedSessionRootMirrorWhenParallel,
        sharedSessionRootMirroringSuppressed = model.SharedSessionRootMirroringSuppressed,
        sessionRootIsCanonicalLiveState = model.SessionRootIsCanonicalLiveState,
        canonicalLiveStateHint = model.CanonicalLiveStateHint,
        indexJsonPath = model.IndexJsonPath,
        warnings = model.Warnings.Select(w => new
        {
            w.Code,
            w.Message,
            w.LeaseId,
            w.TaskId,
        }),
        workers = model.Workers.Select(MapWorker),
    };

    public static object MapWorker(ParallelWorkerMirrorEntry w) => new
    {
        taskId = w.TaskId,
        taskTitle = w.TaskTitle,
        executionSessionId = w.ExecutionSessionId,
        leaseId = w.LeaseId,
        hermesSessionId = w.HermesSessionId,
        liveMirrorPath = w.LiveMirrorPath,
        liveMarkdownPath = w.LiveMarkdownPath,
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
        mergeReadiness = MapReadiness(w.MergeReadiness),
        mergeConflict = MapConflict(w.MergeConflict),
        registryCollision = w.RegistryCollision is null
            ? null
            : new
            {
                w.RegistryCollision.OccupyingLeaseId,
                w.RegistryCollision.OccupyingTaskId,
                w.RegistryCollision.OccupyingMirrorRoot,
            },
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
    };

    private static object? MapReadiness(WorkerMergeReadiness? r) =>
        r is null
            ? null
            : new
            {
                executionSessionId = r.ExecutionSessionId,
                r.WorktreePath,
                r.LiveMirrorPath,
                r.MergeTargetWorkspaceRoot,
                r.MergeTargetBranch,
                r.HeadCommit,
                r.BaseCommit,
                r.IsDirty,
                r.ChangedFilesCount,
                r.ChangedFilesSummary,
                r.VerificationPassed,
                r.TestsRun,
                r.VerificationSummary,
                gitConvergence = r.GitConvergence is null
                    ? null
                    : new
                    {
                        r.GitConvergence.Succeeded,
                        r.GitConvergence.Strategy,
                        r.GitConvergence.DestinationPreviousHead,
                        r.GitConvergence.DestinationNewHead,
                        appliedFiles = r.GitConvergence.AppliedFiles,
                        r.GitConvergence.ErrorMessage,
                    },
            };

    private static object? MapConflict(MergeConflictDetail? c) =>
        c is null
            ? null
            : new
            {
                c.Category,
                c.Reason,
                c.ConflictFiles,
            };
}
