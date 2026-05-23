namespace JoyZoning.Domain.Orchestration;

public sealed record SessionAuthoritySnapshot(
    string Profile,
    string ProfileLabel,
    bool AutopilotEnabled);

public sealed record ParallelWorkersResponse(
    Guid SessionId,
    string SessionWorkspaceRoot,
    DateTimeOffset UpdatedAt,
    bool ParallelActive,
    string LiveMirrorMode,
    bool DisableSharedSessionRootMirrorWhenParallel,
    bool SharedSessionRootMirroringSuppressed,
    bool SessionRootIsCanonicalLiveState,
    string CanonicalLiveStateHint,
    string? IndexJsonPath,
    SessionAuthoritySnapshot Authority,
    IReadOnlyList<ParallelWorkerMirrorEntry> Workers,
    IReadOnlyList<MirrorObservabilityWarning> Warnings);

public sealed record ParallelWorkerMirrorEntry(
    Guid TaskId,
    string TaskTitle,
    Guid? ExecutionSessionId,
    Guid LeaseId,
    string? HermesSessionId,
    string? LiveMirrorPath,
    string? LiveMarkdownPath,
    string HealthState,
    string LifecycleStatus,
    DateTimeOffset? LastMirroredAt,
    long KanbanRevision,
    long KanbanPushedRevision,
    string KanbanStatus,
    string LeaseStatus,
    string? WorktreePath,
    bool IsSharedSessionRootMirror,
    MirrorCollisionInfo? RegistryCollision,
    string MergeState,
    WorkerMergeReadiness? MergeReadiness,
    MergeConflictDetail? MergeConflict,
    OperatorDecisionSummary DecisionSummary,
    OperatorActionGuardrails ApproveGuardrails,
    OperatorActionGuardrails RevokeGuardrails,
    string RecommendedModeSlug,
    IReadOnlyList<ModeTransitionHint> AvailableModeTransitions,
    string AuthorityProfileSlug,
    AuthorityAutopilotDecision? Authority);

public sealed record MirrorCollisionInfo(
    Guid OccupyingLeaseId,
    Guid? OccupyingTaskId,
    string? OccupyingMirrorRoot);

public sealed record MirrorObservabilityWarning(
    string Code,
    string Message,
    Guid? LeaseId,
    Guid? TaskId);
