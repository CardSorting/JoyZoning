namespace JoyZoning.Adapters.Workspace;

public sealed record GitWorktreeSummary(
    string? HeadCommit,
    string? BranchName,
    string? BaseCommit,
    bool IsDirty,
    bool HasUnmergedConflicts,
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<string> ConflictPaths);
