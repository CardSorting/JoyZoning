using JoyZoning.Domain.Configuration;

namespace JoyZoning.Agents.Hermes;

/// <summary>Mutable Hermes connection settings — updated when user saves Settings without restart.</summary>
public class HermesRuntimeSettings
{
    private readonly object _lock = new();

    public string InstallRoot { get; private set; }
    public string ApiBaseUrl { get; private set; }
    public string DashboardBaseUrl { get; private set; }
    public string Profile { get; private set; }
    public string? ApiKey { get; private set; }
    public bool AutoStartGateway { get; private set; }

    public HermesRuntimeSettings(HermesOptions defaults)
    {
        InstallRoot = defaults.InstallRoot;
        ApiBaseUrl = defaults.ApiBaseUrl;
        DashboardBaseUrl = defaults.DashboardBaseUrl;
        Profile = defaults.Profile;
        ApiKey = defaults.ApiKey;
        AutoStartGateway = defaults.AutoStartGateway;
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
            if (!string.IsNullOrWhiteSpace(installRoot)) InstallRoot = installRoot;
            if (!string.IsNullOrWhiteSpace(apiBaseUrl)) ApiBaseUrl = apiBaseUrl.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(dashboardBaseUrl)) DashboardBaseUrl = dashboardBaseUrl.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(profile)) Profile = profile;
            if (apiKey is not null) ApiKey = apiKey;
        }
    }

    public Uri ApiUri => new($"{ApiBaseUrl.TrimEnd('/')}/");
}
