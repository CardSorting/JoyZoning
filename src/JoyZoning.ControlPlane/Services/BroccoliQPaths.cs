using JoyZoning.Domain.Configuration;

namespace JoyZoning.ControlPlane.Services;

internal static class BroccoliQPaths
{
    public static string? ResolveRepoRoot(BroccoliQOptions options, string? contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(options.RepoRoot))
            return options.RepoRoot;

        var envRoot = Environment.GetEnvironmentVariable("JOYZONING_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
            return envRoot;

        if (!string.IsNullOrWhiteSpace(contentRootPath) && Directory.Exists(contentRootPath))
        {
            var fromContent = FindBroccoliqRoot(contentRootPath);
            if (fromContent is not null)
                return fromContent;
        }

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var found = FindBroccoliqRoot(dir);
            if (found is not null)
                return found;

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        return null;
    }

    public static string? ResolveJoyBridgeScript(string? repoRoot) =>
        repoRoot is null
            ? null
            : Path.Combine(repoRoot, "broccoliq", "worker", "joy-bridge.mjs");

    public static bool IsDistBuilt(string? repoRoot)
    {
        if (repoRoot is null) return false;
        return File.Exists(Path.Combine(repoRoot, "broccoliq", "dist", "infrastructure", "index.js"));
    }

    public static void EnsureDatabaseDirectory(string? databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            return;

        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    private static string? FindBroccoliqRoot(string startDir)
    {
        var dir = startDir;
        for (var i = 0; i < 8; i++)
        {
            var marker = Path.Combine(dir, "broccoliq", "package.json");
            if (File.Exists(marker))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        return null;
    }
}
