using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Agents.Hermes;

/// <summary>Best-effort deduplication when pushing tasks to Hermes kanban.</summary>
public static class KanbanTaskMatcher
{
    public static string? FindExistingKanbanId(
        string title,
        string workspaceRoot,
        IReadOnlyList<HermesKanbanTaskSnapshot> remote)
    {
        if (remote.Count == 0)
            return null;

        foreach (var snap in remote)
        {
            if (!TitleMatches(title, snap.Title))
                continue;

            if (!string.IsNullOrEmpty(snap.WorkspacePath)
                && !WorkspacePaths.IsSameOrChildWorkspace(snap.WorkspacePath, workspaceRoot))
                continue;

            return snap.Id;
        }

        return null;
    }

    private static bool TitleMatches(string local, string remote) =>
        string.Equals(local.Trim(), remote.Trim(), StringComparison.OrdinalIgnoreCase);
}
