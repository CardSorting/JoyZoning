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

        var normalizedRoot = NormalizePath(workspaceRoot);

        foreach (var snap in remote)
        {
            if (!TitleMatches(title, snap.Title))
                continue;

            if (!string.IsNullOrEmpty(snap.WorkspacePath))
            {
                try
                {
                    var snapRoot = NormalizePath(snap.WorkspacePath);
                    if (!snapRoot.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                        && !snapRoot.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                catch
                {
                    continue;
                }
            }

            return snap.Id;
        }

        return null;
    }

    private static bool TitleMatches(string local, string remote) =>
        string.Equals(local.Trim(), remote.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
}
