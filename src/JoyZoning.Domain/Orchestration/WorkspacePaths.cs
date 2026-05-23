namespace JoyZoning.Domain.Orchestration;

/// <summary>Canonical workspace path comparison — one physical folder, one JoyZoning identity.</summary>
public static class WorkspacePaths
{
    public static bool TryNormalize(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            normalized = NormalizeComparable(Path.GetFullPath(path.Trim()));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string Normalize(string path) =>
        NormalizeComparable(Path.GetFullPath(path.Trim()));

    public static bool EqualsNormalized(string a, string b) =>
        TryNormalize(a, out var normA)
        && TryNormalize(b, out var normB)
        && normA.Equals(normB, StringComparison.OrdinalIgnoreCase);

    public static bool IsSameOrChildWorkspace(string candidatePath, string workspaceRoot)
    {
        if (!TryNormalize(candidatePath, out var candidate)
            || !TryNormalize(workspaceRoot, out var root))
            return false;

        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>macOS resolves /var and /tmp through /private; align before comparing.</summary>
    public static string NormalizeComparable(string fullPath)
    {
        var trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!OperatingSystem.IsMacOS())
            return trimmed;

        if (trimmed.StartsWith("/private/var/", StringComparison.Ordinal))
            return "/var/" + trimmed["/private/var/".Length..];
        if (trimmed.StartsWith("/private/tmp/", StringComparison.Ordinal))
            return "/tmp/" + trimmed["/private/tmp/".Length..];
        if (trimmed.StartsWith("/private/", StringComparison.Ordinal))
            return trimmed["/private".Length..];
        return trimmed;
    }
}
