using System.Collections.Concurrent;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Serializes workspace-scoped identity mutations (session create, kanban sync, consolidation)
/// so parallel callers cannot fork duplicate sessions or tasks for the same folder.
/// </summary>
public sealed class WorkspaceIdentityCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<T> RunExclusiveAsync<T>(
        string workspaceRoot,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var key = WorkspacePaths.TryNormalize(workspaceRoot, out var norm)
            ? norm
            : workspaceRoot.Trim();

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
