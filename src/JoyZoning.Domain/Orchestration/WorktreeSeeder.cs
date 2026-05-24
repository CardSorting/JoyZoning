namespace JoyZoning.Domain.Orchestration;

/// <summary>Copies canonical session workspace files into a fresh worker sandbox.</summary>
public static class WorktreeSeeder
{
    private static readonly string[] SkipPrefixSegments =
    [
        ".git",
        "node_modules",
    ];

    private static readonly string[] SkipJoyZoningSubdirs =
    [
        "worktrees",
        "live",
    ];

    public sealed record SeedResult(int FilesCopied, bool SkippedExistingContent);

    public static bool HasProjectFoundation(string sessionRoot)
    {
        if (string.IsNullOrWhiteSpace(sessionRoot) || !Directory.Exists(sessionRoot))
            return false;

        return File.Exists(Path.Combine(sessionRoot, "package.json"))
            || File.Exists(Path.Combine(sessionRoot, "app.json"))
            || Directory.Exists(Path.Combine(sessionRoot, "app"));
    }

    public static SeedResult TrySeedFromSession(string sessionRoot, string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(sessionRoot) || !Directory.Exists(sessionRoot))
            return new SeedResult(0, SkippedExistingContent: false);

        if (WorktreeHasProjectContent(worktreePath))
            return new SeedResult(0, SkippedExistingContent: true);

        Directory.CreateDirectory(worktreePath);

        var sessionFull = Path.GetFullPath(sessionRoot.Trim());
        var worktreeFull = Path.GetFullPath(worktreePath.Trim());
        var copied = 0;

        foreach (var sourceFile in Directory.EnumerateFiles(sessionFull, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sessionFull, sourceFile).Replace('\\', '/');
            if (ShouldSkipRelativePath(rel))
                continue;

            var target = Path.Combine(worktreeFull, rel);
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            File.Copy(sourceFile, target, overwrite: true);
            copied++;
        }

        return new SeedResult(copied, SkippedExistingContent: false);
    }

    internal static bool WorktreeHasProjectContent(string worktreePath)
    {
        if (!Directory.Exists(worktreePath))
            return false;

        foreach (var file in Directory.EnumerateFiles(worktreePath, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(worktreePath, file).Replace('\\', '/');
            if (rel.Equals(".joyzoning/context.json", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ShouldSkipRelativePath(rel))
                continue;

            return true;
        }

        return false;
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

        if (segments.Length >= 2
            && segments[0].Equals(".joyzoning", StringComparison.OrdinalIgnoreCase)
            && SkipJoyZoningSubdirs.Contains(segments[1], StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
