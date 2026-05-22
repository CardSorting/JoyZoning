namespace JoyZoning.App.Services;

/// <summary>Discovers the single diet-hermes checkout JoyZoning uses.</summary>
public static class OnboardingPathDiscovery
{
    public static IReadOnlyList<string> DiscoverInstallRoots(string? current = null)
    {
        var found = new List<string>();

        void TryAdd(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                path = Path.GetFullPath(path.Trim());
            }
            catch
            {
                return;
            }

            if (!Directory.Exists(path)) return;
            if (found.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                return;

            if (OnboardingEvaluator.ValidateInstallRoot(path).IsValid)
                found.Add(path);
        }

        TryAdd(HermesInstallPaths.CanonicalInstallRoot);
        TryAdd(current);

        foreach (var envKey in new[] { "HERMES_INSTALL_ROOT", "DIET_HERMES_ROOT" })
            TryAdd(Environment.GetEnvironmentVariable(envKey));

        var prefs = OnboardingPreferences.Load();
        TryAdd(prefs.DietHermesInstallPath);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        TryAdd(Path.Combine(home, "Downloads", "diet-hermes-main-master"));

        var downloads = Path.Combine(home, "Downloads");
        if (Directory.Exists(downloads))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(downloads))
                {
                    var name = Path.GetFileName(dir);
                    if (name.Contains("diet-hermes", StringComparison.OrdinalIgnoreCase))
                        TryAdd(dir);
                }
            }
            catch
            {
                // ignore permission errors
            }
        }

        return found;
    }

    public static string? BestGuess(string? current = null)
    {
        var canonical = HermesInstallPaths.CanonicalInstallRoot;
        if (Directory.Exists(canonical)
            && OnboardingEvaluator.ValidateInstallRoot(canonical).IsValid)
            return Path.GetFullPath(canonical);

        var all = DiscoverInstallRoots(current);
        if (all.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(current))
        {
            var normalized = Path.GetFullPath(current.Trim());
            var match = all.FirstOrDefault(p =>
                string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return all[0];
    }
}
