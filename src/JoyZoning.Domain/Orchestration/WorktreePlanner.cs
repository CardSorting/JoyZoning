namespace JoyZoning.Domain.Orchestration;

/// <summary>Plans isolated git worktree/branch metadata for a card lease.</summary>
public static class WorktreePlanner
{
    private static readonly HashSet<char> SafeHex = "0123456789abcdef".ToHashSet();

    public static bool TryPlan(
        string workspaceRoot,
        Guid cardId,
        out string worktreePath,
        out string branchName,
        out string? error)
    {
        worktreePath = string.Empty;
        branchName = string.Empty;
        error = null;

        if (cardId == Guid.Empty)
        {
            error = "Invalid card id.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            error = "Workspace root is required.";
            return false;
        }

        string workspaceFull;
        try
        {
            workspaceFull = Path.GetFullPath(workspaceRoot.Trim());
        }
        catch
        {
            error = "Workspace root is not a valid path.";
            return false;
        }

        if (!Directory.Exists(workspaceFull))
        {
            error = "Workspace root does not exist.";
            return false;
        }

        var shortId = SanitizeCardIdSegment(cardId);
        if (shortId is null)
        {
            error = "Card id cannot be used for worktree paths.";
            return false;
        }

        branchName = $"joyzoning/card-{shortId}";
        var worktreesRoot = NormalizeComparablePath(
            Path.GetFullPath(Path.Combine(workspaceFull, ".joyzoning", "worktrees")));
        worktreePath = NormalizeComparablePath(
            Path.GetFullPath(Path.Combine(worktreesRoot, shortId)));

        if (!worktreePath.StartsWith(worktreesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !worktreePath.Equals(worktreesRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "Worktree path would escape the workspace sandbox.";
            return false;
        }

        return true;
    }

    /// <summary>macOS resolves /var and /tmp through /private; align before sandbox checks.</summary>
    private static string NormalizeComparablePath(string fullPath)
    {
        var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!OperatingSystem.IsMacOS())
            return trimmed;

        if (trimmed.StartsWith("/private/var/", StringComparison.Ordinal))
            return "/var/" + trimmed["/private/var/".Length..];
        if (trimmed.StartsWith("/private/tmp/", StringComparison.Ordinal))
            return "/tmp/" + trimmed["/private/tmp/".Length..];
        return trimmed;
    }

    public static (string WorktreePath, string BranchName) Plan(string workspaceRoot, Guid cardId)
    {
        if (!TryPlan(workspaceRoot, cardId, out var worktreePath, out var branchName, out var error))
            throw LeaseOrchestrationException.BadRequest(error!);

        return (worktreePath, branchName);
    }

    private static string? SanitizeCardIdSegment(Guid cardId)
    {
        var hex = cardId.ToString("N");
        if (hex.Length < 8 || hex.Any(c => !SafeHex.Contains(char.ToLowerInvariant(c))))
            return null;

        return hex[..8].ToLowerInvariant();
    }
}
