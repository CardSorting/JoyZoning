namespace JoyZoning.Domain.Orchestration;

/// <summary>Applies worker output into the canonical session git workspace.</summary>
public interface IWorkspaceGitMerger
{
    Task<WorkspaceConvergenceResult> PreflightAsync(
        WorkspaceConvergenceRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkspaceConvergenceResult> ConvergeAsync(
        WorkspaceConvergenceRequest request,
        CancellationToken cancellationToken = default);
}
