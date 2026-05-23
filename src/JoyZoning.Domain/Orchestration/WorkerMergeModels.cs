namespace JoyZoning.Domain.Orchestration;

public sealed record WorkerMergeReadiness(
    Guid? ExecutionSessionId,
    string? WorktreePath,
    string? LiveMirrorPath,
    string MergeTargetWorkspaceRoot,
    string? MergeTargetBranch,
    string? HeadCommit,
    string? BaseCommit,
    bool IsDirty,
    int ChangedFilesCount,
    IReadOnlyList<string> ChangedFilesSummary,
    bool? VerificationPassed,
    bool? TestsRun,
    string? VerificationSummary);

public sealed record MergeConflictDetail(
    string Category,
    string Reason,
    IReadOnlyList<string> ConflictFiles);

public sealed record MergeQueueResponse(
    Guid SessionId,
    string SessionWorkspaceRoot,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ParallelWorkerMirrorEntry> ReadyToMerge,
    IReadOnlyList<ParallelWorkerMirrorEntry> MergeConflicts,
    IReadOnlyList<ParallelWorkerMirrorEntry> CompletedWorkers,
    IReadOnlyList<ParallelWorkerMirrorEntry> RevokedAbandoned,
    IReadOnlyList<ParallelWorkerMirrorEntry> AllWorkers,
    IReadOnlyList<MirrorObservabilityWarning> Warnings);
