namespace JoyZoning.App.Services;

/// <summary>
/// Single diet-hermes install root. Manager vs executor are session roles on the same
/// Hermes gateway — coordinated through JoyZoning kanban, not a second Hermes checkout.
/// </summary>
public static class HermesInstallPaths
{
    /// <summary>Canonical diet-hermes checkout (master install).</summary>
    public static string CanonicalInstallRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "diet-hermes-main-master");

    public static bool IsDietHermesCheckout(string path)
    {
        if (!Directory.Exists(path))
            return false;

        return File.Exists(Path.Combine(path, "pyproject.toml"))
               && (
                   Directory.Exists(Path.Combine(path, "broccolidb"))
                   || Directory.Exists(Path.Combine(path, "plugins", "joyzoning_governance"))
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
