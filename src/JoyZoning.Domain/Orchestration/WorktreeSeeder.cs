namespace JoyZoning.Domain.Orchestration;

/// <summary>Foundation detection and path skip rules for canonical workspace execution.</summary>
public static class WorktreeSeeder
{
    private static readonly string[] SkipPrefixSegments =
    [
        ".git",
        "node_modules",
    ];

    private const string JoyZoningDir = ".joyzoning";

    public sealed record SeedResult(int FilesCopied, bool SkippedExistingContent);

    public static bool HasProjectFoundation(string sessionRoot)
    {
        if (string.IsNullOrWhiteSpace(sessionRoot) || !Directory.Exists(sessionRoot))
            return false;

        return File.Exists(Path.Combine(sessionRoot, "package.json"))
            || File.Exists(Path.Combine(sessionRoot, "app.json"))
            || Directory.Exists(Path.Combine(sessionRoot, "app"));
    }

    public static bool ShouldSkipRelativePath(string rel)
    {
        if (string.IsNullOrWhiteSpace(rel))
            return true;

        var normalized = rel.Replace('\\', '/').TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return true;

        if (SkipPrefixSegments.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
            return true;

        if (segments[0].Equals(JoyZoningDir, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
