namespace JoyZoning.Agents.Hermes;

/// <summary>Reads secrets from Hermes profile layout (~/.hermes, profiles, default profile).</summary>
public static class HermesProfileEnv
{
    public static string? TryReadApiServerKey(string? profile)
    {
        foreach (var envPath in ResolveEnvFileCandidates(profile))
        {
            var key = ReadApiServerKeyFromFile(envPath);
            if (!string.IsNullOrWhiteSpace(key))
                return key;
        }

        return null;
    }

    public static IEnumerable<string> ResolveEnvFileCandidates(string? profile)
    {
        var trimmedProfile = string.IsNullOrWhiteSpace(profile) ? "default" : profile.Trim();
        var hermesHome = ResolveHermesHome(trimmedProfile);

        if (IsProfileHomeDirectory(hermesHome, trimmedProfile))
        {
            yield return Path.Combine(hermesHome, ".env");
            yield break;
        }

        if (string.Equals(trimmedProfile, "default", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(hermesHome, ".env");
            yield break;
        }

        yield return Path.Combine(hermesHome, "profiles", trimmedProfile, ".env");
        yield return Path.Combine(hermesHome, ".env");
    }

    internal static string ResolveHermesHome(string profile)
    {
        var envHome = Environment.GetEnvironmentVariable("HERMES_HOME");
        if (!string.IsNullOrWhiteSpace(envHome))
        {
            var normalized = Path.GetFullPath(envHome);
            if (IsProfileHomeDirectory(normalized, profile))
                return normalized;
            if (Directory.Exists(Path.Combine(normalized, "profiles", profile)))
                return normalized;
            return normalized;
        }

        var userHome = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrWhiteSpace(userHome))
            userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Path.Combine(userHome, ".hermes");
    }

    private static bool IsProfileHomeDirectory(string hermesHome, string profile)
    {
        if (!hermesHome.Contains($"{Path.DirectorySeparatorChar}profiles{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            return false;

        return hermesHome.EndsWith($"{Path.DirectorySeparatorChar}profiles{Path.DirectorySeparatorChar}{profile}", StringComparison.OrdinalIgnoreCase)
            || hermesHome.EndsWith($"{Path.DirectorySeparatorChar}profiles{Path.DirectorySeparatorChar}{profile}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadApiServerKeyFromFile(string envPath)
    {
        if (!File.Exists(envPath))
            return null;

        foreach (var line in File.ReadLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;
            if (!trimmed.StartsWith("API_SERVER_KEY=", StringComparison.Ordinal))
                continue;

            return Unquote(trimmed["API_SERVER_KEY=".Length..].Trim());
        }

        return null;
    }

    private static string? Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            value = value[1..^1];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
