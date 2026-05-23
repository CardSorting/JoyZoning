namespace JoyZoning.Agents.Hermes;

/// <summary>Discover Hermes profiles and model settings under <c>~/.hermes</c>.</summary>
public static class HermesProfileCatalog
{
    public static string ResolveHermesHome() => HermesProfileEnv.ResolveHermesHome("default");

    public static IReadOnlyList<HermesProfileSummary> ListProfiles(string? hermesHome = null)
    {
        hermesHome ??= ResolveHermesHome();
        var profiles = new List<HermesProfileSummary>();

        profiles.Add(SummarizeProfile("default", hermesHome));

        var profilesDir = Path.Combine(hermesHome, "profiles");
        if (Directory.Exists(profilesDir))
        {
            foreach (var dir in Directory.EnumerateDirectories(profilesDir).OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(name))
                    profiles.Add(SummarizeProfile(name, hermesHome));
            }
        }

        return profiles;
    }

    public static HermesProfileSummary? FindProfile(string profileName, string? hermesHome = null)
    {
        hermesHome ??= ResolveHermesHome();
        return ListProfiles(hermesHome)
            .FirstOrDefault(p => p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
    }

    public static HermesProfileLinkDiagnostic DiagnoseLink(
        string joyZoningProfile,
        string referenceProfile = "default",
        string? hermesHome = null)
    {
        hermesHome ??= ResolveHermesHome();
        var linked = FindProfile(joyZoningProfile, hermesHome)
            ?? new HermesProfileSummary(joyZoningProfile, ProfileConfigPath(joyZoningProfile, hermesHome), new HermesModelConfig());
        var reference = FindProfile(referenceProfile, hermesHome)
            ?? new HermesProfileSummary(referenceProfile, ProfileConfigPath(referenceProfile, hermesHome), new HermesModelConfig());

        var modelsMatch = ModelsEquivalent(linked.Model, reference.Model);
        return new HermesProfileLinkDiagnostic(
            LinkedProfile: linked,
            ReferenceProfile: reference,
            ModelsMatch: modelsMatch,
            Recommendation: modelsMatch
                ? null
                : $"JoyZoning uses profile '{joyZoningProfile}' ({linked.Model.Describe()}) but '{referenceProfile}' has {reference.Model.Describe()}. Run: jz config hermes sync-model --from {referenceProfile}");
    }

    private static bool ModelsEquivalent(HermesModelConfig a, HermesModelConfig b)
    {
        if (!a.IsConfigured && !b.IsConfigured)
            return true;

        return string.Equals(a.Describe(), b.Describe(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Provider ?? "", b.Provider ?? "", StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.BaseUrl ?? "", b.BaseUrl ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static HermesProfileSummary SummarizeProfile(string name, string hermesHome)
    {
        var configPath = ProfileConfigPath(name, hermesHome);
        var model = HermesConfigYamlReader.ReadModel(configPath);
        var envPath = ProfileEnvPath(name, hermesHome);
        var hasApiKey = !string.IsNullOrWhiteSpace(HermesProfileEnv.TryReadApiServerKey(name));
        var apiPort = ReadEnvInt(envPath, "API_SERVER_PORT") ?? 8642;
        var apiEnabled = ReadEnvBool(envPath, "API_SERVER_ENABLED");

        return new HermesProfileSummary(
            name,
            configPath,
            model,
            EnvPath: envPath,
            HasApiServerKey: hasApiKey,
            ApiServerPort: apiPort,
            ApiServerEnabled: apiEnabled);
    }

    public static string ProfileConfigPath(string profileName, string hermesHome)
    {
        if (profileName.Equals("default", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(hermesHome, "config.yaml");

        return Path.Combine(hermesHome, "profiles", profileName, "config.yaml");
    }

    public static string ProfileEnvPath(string profileName, string hermesHome)
    {
        if (profileName.Equals("default", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(hermesHome, ".env");

        return Path.Combine(hermesHome, "profiles", profileName, ".env");
    }

    private static int? ReadEnvInt(string envPath, string key)
    {
        var value = ReadEnvValue(envPath, key);
        return int.TryParse(value, out var n) ? n : null;
    }

    private static bool? ReadEnvBool(string envPath, string key)
    {
        var value = ReadEnvValue(envPath, key);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value == "1";
    }

    private static string? ReadEnvValue(string envPath, string key)
    {
        if (!File.Exists(envPath))
            return null;

        foreach (var line in File.ReadLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;
            if (!trimmed.StartsWith($"{key}=", StringComparison.Ordinal))
                continue;
            return trimmed[(key.Length + 1)..].Trim().Trim('"', '\'');
        }

        return null;
    }
}

public sealed record HermesProfileSummary(
    string Name,
    string ConfigPath,
    HermesModelConfig Model,
    string? EnvPath = null,
    bool HasApiServerKey = false,
    int ApiServerPort = 8642,
    bool? ApiServerEnabled = null)
{
    public bool ConfigExists => File.Exists(ConfigPath);
}

public sealed record HermesProfileLinkDiagnostic(
    HermesProfileSummary LinkedProfile,
    HermesProfileSummary ReferenceProfile,
    bool ModelsMatch,
    string? Recommendation);
