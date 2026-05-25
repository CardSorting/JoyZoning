using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>Deterministic git convergence failure for lease accept-merge tests.</summary>
public sealed class FailingWorkspaceGitMerger : IWorkspaceGitMerger
{
    public const string SimulatedConflictMessage = "simulated git convergence conflict (test double)";

    public Task<WorkspaceConvergenceResult> PreflightAsync(
        WorkspaceConvergenceRequest request,
        CancellationToken cancellationToken = default) =>
        ConvergeAsync(request, cancellationToken);

    public Task<WorkspaceConvergenceResult> ConvergeAsync(
        WorkspaceConvergenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var result = new WorkspaceConvergenceResult(
            Succeeded: false,
            HadConflicts: true,
            Strategy: "test_double",
            ErrorMessage: SimulatedConflictMessage,
            SourceWorktreePath: request.WorkerWorktreePath,
            SourceBranch: request.WorkerBranchName,
            SourceHeadCommit: null,
            DestinationWorkspaceRoot: request.DestinationWorkspaceRoot,
            DestinationBranch: null,
            DestinationPreviousHead: null,
            DestinationNewHead: null,
            ChangedFiles: Array.Empty<string>(),
            ConflictFiles: new[] { "worker-change.txt" },
            CompletedAt: completedAt);
        return Task.FromResult(result);
    }
}
