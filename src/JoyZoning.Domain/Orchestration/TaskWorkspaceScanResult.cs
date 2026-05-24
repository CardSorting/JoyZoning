namespace JoyZoning.Domain.Orchestration;

public sealed record TaskWorkspaceScanResult(
    Guid TaskId,
    string WorkspacePath,
    string? ExpectedBranchName,
    string? CurrentBranch,
    bool BranchMatches,
    bool HasChanges,
    IReadOnlyList<string> ChangedFiles,
    string? LastCommit,
    DateTimeOffset ScannedAt,
    bool HasUncommittedChanges,
    IReadOnlyList<string> StagedFiles,
    IReadOnlyList<string> UntrackedFiles);
