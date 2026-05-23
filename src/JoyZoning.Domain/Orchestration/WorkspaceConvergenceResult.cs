namespace JoyZoning.Domain.Orchestration;

/// <summary>Outcome of applying a worker worktree/branch into the canonical session workspace.</summary>
public sealed record WorkspaceConvergenceResult(
    bool Succeeded,
    bool HadConflicts,
    string Strategy,
    string? ErrorMessage,
    string SourceWorktreePath,
    string? SourceBranch,
    string? SourceHeadCommit,
    string DestinationWorkspaceRoot,
    string? DestinationBranch,
    string? DestinationPreviousHead,
    string? DestinationNewHead,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> ConflictFiles,
    DateTimeOffset CompletedAt);

public sealed record WorkspaceConvergenceRequest(
    string DestinationWorkspaceRoot,
    string WorkerWorktreePath,
    string WorkerBranchName,
    bool DryRun = false,
    bool AllowDirtyDestination = false);

public sealed record GitConvergenceSummary(
    bool Succeeded,
    string Strategy,
    string? DestinationPreviousHead,
    string? DestinationNewHead,
    IReadOnlyList<string> AppliedFiles,
    string? ErrorMessage);
