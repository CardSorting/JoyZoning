namespace JoyZoning.Domain.Orchestration;

public sealed record WorkerMergeReadiness(
    Guid? ExecutionSessionId,
    string? WorktreePath,
    string MergeTargetWorkspaceRoot,
    string? MergeTargetBranch,
    string? HeadCommit,
    string? BaseCommit,
    bool IsDirty,
    int ChangedFilesCount,
    IReadOnlyList<string> ChangedFilesSummary,
    bool? VerificationPassed,
    bool? TestsRun,
    string? VerificationSummary,
    GitConvergenceSummary? GitConvergence = null);

public sealed record MergeConflictDetail(
    string Category,
    string Reason,
    IReadOnlyList<string> ConflictFiles);

public sealed record MergeQueueResponse(
    Guid SessionId,
    string SessionWorkspaceRoot,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ParallelWorkerEntry> ReadyToMerge,
    IReadOnlyList<ParallelWorkerEntry> MergeConflicts,
    IReadOnlyList<ParallelWorkerEntry> CompletedWorkers,
    IReadOnlyList<ParallelWorkerEntry> RevokedAbandoned,
    IReadOnlyList<ParallelWorkerEntry> AllWorkers,
    IReadOnlyList<WorkerObservabilityWarning> Warnings);
