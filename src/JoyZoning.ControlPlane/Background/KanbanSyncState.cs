using System.Collections.Concurrent;
using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Background;

public class KanbanSyncState
{
    public DateTimeOffset? LastSyncAt { get; set; }
    public string LastMessage { get; set; } = "Not synced yet";
    public KanbanSyncOutcome? LastOutcome { get; set; }
    public DateTimeOffset? LastAuthFailureAt { get; set; }

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSyncByWorkspace =
        new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset? GetLastSyncForWorkspace(string workspaceRoot)
    {
        var key = WorkspacePaths.TryNormalize(workspaceRoot, out var norm) ? norm : workspaceRoot.Trim();
        return _lastSyncByWorkspace.TryGetValue(key, out var at) ? at : null;
    }

    public void MarkWorkspaceSynced(string workspaceRoot, DateTimeOffset at)
    {
        var key = WorkspacePaths.TryNormalize(workspaceRoot, out var norm) ? norm : workspaceRoot.Trim();
        _lastSyncByWorkspace[key] = at;
        LastSyncAt = at;
    }
}
