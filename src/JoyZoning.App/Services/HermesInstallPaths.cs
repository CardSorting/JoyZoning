namespace JoyZoning.App.Services;

/// <summary>
/// Single diet-hermes install root. Manager vs executor are session roles on the same
/// Hermes gateway — coordinated through JoyZoning kanban, not a second Hermes checkout.
/// </summary>
public static class HermesInstallPaths
{
    private const string LegacyShimMarker = "LEGACY_RUNTIME_SHIM.md";

    private static string? FindMonorepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "JoyZoning.sln")))
            {
                return dir;
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    public static bool IsLegacyRuntimeShimPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        return normalized.EndsWith("apps/agent-runtime", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/apps/agent-runtime/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Canonical external diet-hermes checkout — never the embedded LegacyRuntimeShim.</summary>
    public static string CanonicalInstallRoot
    {
        get
        {
            var monorepoRoot = FindMonorepoRoot();
            if (monorepoRoot != null)
            {
                var siblingHermes = Path.Combine(
                    Directory.GetParent(monorepoRoot)?.FullName ?? monorepoRoot,
                    "diet-hermes-main-master");
                if (IsDietHermesCheckout(siblingHermes) && !IsLegacyRuntimeShimPath(siblingHermes))
                    return siblingHermes;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "diet-hermes-main-master");
        }
    }

    public static bool IsDietHermesCheckout(string path)
    {
        if (!Directory.Exists(path) || IsLegacyRuntimeShimPath(path))
            return false;

        if (File.Exists(Path.Combine(path, LegacyShimMarker)))
            return false;

        return File.Exists(Path.Combine(path, "pyproject.toml"))
               && (
                   Directory.Exists(Path.Combine(path, "broccolidb"))
                   || Directory.Exists(Path.Combine(path, "plugins", "joyzoning_governance"))
                   || Directory.Exists(Path.Combine(path, "agent", "joyzoning"))
                   || File.Exists(Path.Combine(path, "tools", "broccolidb.py"))
                   || File.Exists(Path.Combine(path, "run_agent.py")));
    }

    public static string? FindHermesCliInCheckout(string installRoot)
    {
        foreach (var venv in new[] { ".venv", "venv" })
        {
            var cli = Path.Combine(installRoot, venv, "bin", "hermes");
            if (File.Exists(cli))
                return cli;
        }

        return null;
    }
}
