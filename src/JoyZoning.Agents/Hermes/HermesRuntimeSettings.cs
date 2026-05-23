using JoyZoning.Domain.Configuration;

namespace JoyZoning.Agents.Hermes;

/// <summary>Mutable Hermes connection settings — thread-safe snapshot reads.</summary>
public class HermesRuntimeSettings
{
    private readonly object _lock = new();
    private HermesConnectionSnapshot _snapshot;

    public HermesRuntimeSettings(HermesOptions defaults)
    {
        _snapshot = HermesConnectionSnapshot.FromOptions(defaults);
    }

    public HermesConnectionSnapshot GetSnapshot()
    {
        lock (_lock)
            return _snapshot;
    }

    public void Apply(
        string installRoot,
        string apiBaseUrl,
        string dashboardBaseUrl,
        string profile,
        string? apiKey = null)
    {
        lock (_lock)
        {
            var next = _snapshot with
            {
                InstallRoot = string.IsNullOrWhiteSpace(installRoot) ? _snapshot.InstallRoot : installRoot,
                ApiBaseUrl = string.IsNullOrWhiteSpace(apiBaseUrl) ? _snapshot.ApiBaseUrl : apiBaseUrl.TrimEnd('/'),
                DashboardBaseUrl = string.IsNullOrWhiteSpace(dashboardBaseUrl)
                    ? _snapshot.DashboardBaseUrl
                    : dashboardBaseUrl.TrimEnd('/'),
                Profile = string.IsNullOrWhiteSpace(profile) ? _snapshot.Profile : profile,
            };
            if (apiKey is not null)
                next = next with { ApiKey = apiKey };
            _snapshot = next;
        }
    }

    /// <summary>Re-read API_SERVER_KEY from the active profile .env.</summary>
    public bool ReloadApiKeyFromProfile()
    {
        lock (_lock)
        {
            var key = HermesProfileEnv.TryReadApiServerKey(_snapshot.Profile);
            if (string.IsNullOrWhiteSpace(key))
                return false;
            _snapshot = _snapshot with { ApiKey = key };
            return true;
        }
    }
}

public sealed record HermesConnectionSnapshot(
    string InstallRoot,
    string ApiBaseUrl,
    string DashboardBaseUrl,
    string Profile,
    string? ApiKey,
    bool AutoStartGateway)
{
    public Uri ApiUri => new($"{ApiBaseUrl.TrimEnd('/')}/");

    public static HermesConnectionSnapshot FromOptions(HermesOptions opts) =>
        new(
            opts.InstallRoot,
            opts.ApiBaseUrl,
            opts.DashboardBaseUrl,
            opts.Profile,
            opts.ApiKey,
            opts.AutoStartGateway);
}
