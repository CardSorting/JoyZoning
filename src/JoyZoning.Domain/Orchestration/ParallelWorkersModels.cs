using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public sealed record SessionAuthoritySnapshot(
    string Profile,
    string ProfileLabel,
    bool AutopilotEnabled);

/// <summary>Workers and merge state for a workspace (JSDP — canonical workspace only).</summary>
public sealed record ParallelWorkersResponse(
    Guid SessionId,
    string SessionWorkspaceRoot,
    DateTimeOffset UpdatedAt,
    bool ParallelActive,
    string Protocol,
    SessionAuthoritySnapshot Authority,
    IReadOnlyList<ParallelWorkerEntry> Workers,
    IReadOnlyList<WorkerObservabilityWarning> Warnings);

public sealed record ParallelWorkerEntry(
    Guid TaskId,
    string TaskTitle,
    Guid? ExecutionSessionId,
    Guid LeaseId,
    string? HermesSessionId,
    string? WorkspacePath,
    long KanbanRevision,
    long KanbanPushedRevision,
    string KanbanStatus,
    string LeaseStatus,
    string MergeState,
    WorkerMergeReadiness? MergeReadiness,
    MergeConflictDetail? MergeConflict,
    OperatorDecisionSummary DecisionSummary,
    OperatorActionGuardrails ApproveGuardrails,
    OperatorActionGuardrails RevokeGuardrails,
    string RecommendedModeSlug,
    IReadOnlyList<ModeTransitionHint> AvailableModeTransitions,
    string AuthorityProfileSlug,
    AuthorityAutopilotDecision? Authority,
    TaskExecutionMode? TaskExecutionMode = null,
    ExecutionDriver? ExecutionDriver = null,
    string? ExternalAgentName = null,
    string? BranchName = null,
    DateTimeOffset? LastWorkspaceScanAt = null,
    IReadOnlyList<string>? ChangedFiles = null,
    string? GeneratedPrompt = null);

public sealed record WorkerObservabilityWarning(
    string Code,
    string Message,
    Guid? LeaseId,
    Guid? TaskId);
