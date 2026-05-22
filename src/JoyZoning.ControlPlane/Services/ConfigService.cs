using System.Text.Json;
using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Configuration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

public class ConfigService
{
    public const string KeyHermesInstallRoot = "hermes.install_root";
    public const string KeyHermesApiUrl = "hermes.api_base_url";
    public const string KeyHermesDashboardUrl = "hermes.dashboard_base_url";
    public const string KeyHermesProfile = "hermes.profile";
    public const string KeyDashboardToken = "hermes.dashboard_session_token";
    public const string KeyKanbanAutoSync = "kanban.auto_sync_enabled";
    public const string KeyKanbanAutoSyncInterval = "kanban.auto_sync_interval_seconds";

    private readonly IConfigRepository _config;
    private readonly IOptions<HermesOptions> _hermesOptions;
    private readonly KanbanSyncService _kanbanSync;
    private readonly HermesRuntimeSettings _runtime;
    private readonly HermesHttpClient _hermesClient;

    public ConfigService(
        IConfigRepository config,
        IOptions<HermesOptions> hermesOptions,
        KanbanSyncService kanbanSync,
        HermesRuntimeSettings runtime,
        HermesHttpClient hermesClient)
    {
        _config = config;
        _hermesOptions = hermesOptions;
        _kanbanSync = kanbanSync;
        _runtime = runtime;
        _hermesClient = hermesClient;
    }

    public async Task<AppSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _config.GetAllAsync(cancellationToken);
        var opts = _hermesOptions.Value;

        return new AppSettingsDto(
            stored.GetValueOrDefault(KeyHermesInstallRoot)?.Trim('"') ?? opts.InstallRoot,
            stored.GetValueOrDefault(KeyHermesApiUrl)?.Trim('"') ?? opts.ApiBaseUrl,
            stored.GetValueOrDefault(KeyHermesDashboardUrl)?.Trim('"') ?? opts.DashboardBaseUrl,
            stored.GetValueOrDefault(KeyHermesProfile)?.Trim('"') ?? opts.Profile,
            stored.GetValueOrDefault(KeyDashboardToken)?.Trim('"') ?? "",
            await _kanbanSync.IsDashboardReachableAsync(cancellationToken),
            ReadJsonBool(stored, KeyKanbanAutoSync, defaultValue: false),
            ReadJsonInt(stored, KeyKanbanAutoSyncInterval, defaultValue: 120));
    }

    public async Task SaveSettingsAsync(AppSettingsDto settings, CancellationToken cancellationToken = default)
    {
        await _config.SetAsync(KeyHermesInstallRoot, JsonSerializer.Serialize(settings.InstallRoot), cancellationToken);
        await _config.SetAsync(KeyHermesApiUrl, JsonSerializer.Serialize(settings.ApiBaseUrl), cancellationToken);
        await _config.SetAsync(KeyHermesDashboardUrl, JsonSerializer.Serialize(settings.DashboardBaseUrl), cancellationToken);
        await _config.SetAsync(KeyHermesProfile, JsonSerializer.Serialize(settings.Profile), cancellationToken);
        await _config.SetAsync(KeyDashboardToken, JsonSerializer.Serialize(settings.DashboardSessionToken), cancellationToken);
        await _config.SetAsync(KeyKanbanAutoSync, JsonSerializer.Serialize(settings.AutoSyncEnabled), cancellationToken);
        await _config.SetAsync(KeyKanbanAutoSyncInterval, JsonSerializer.Serialize(settings.AutoSyncIntervalSeconds), cancellationToken);

        _kanbanSync.SetDashboardToken(
            string.IsNullOrWhiteSpace(settings.DashboardSessionToken) ? null : settings.DashboardSessionToken);

        _runtime.Apply(
            settings.InstallRoot,
            settings.ApiBaseUrl,
            settings.DashboardBaseUrl,
            settings.Profile);
        _hermesClient.RefreshConnection();
    }

    public async Task BootstrapRuntimeAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _config.GetAllAsync(cancellationToken);
        var opts = _hermesOptions.Value;

        _runtime.Apply(
            ReadJsonString(stored, KeyHermesInstallRoot) ?? opts.InstallRoot,
            ReadJsonString(stored, KeyHermesApiUrl) ?? opts.ApiBaseUrl,
            ReadJsonString(stored, KeyHermesDashboardUrl) ?? opts.DashboardBaseUrl,
            ReadJsonString(stored, KeyHermesProfile) ?? opts.Profile);

        await BootstrapKanbanSyncAsync(cancellationToken);
    }

    private static string? ReadJsonString(IReadOnlyDictionary<string, string> stored, string key)
    {
        if (!stored.TryGetValue(key, out var raw)) return null;
        try { return JsonSerializer.Deserialize<string>(raw); }
        catch { return raw.Trim('"'); }
    }

    private static bool ReadJsonBool(IReadOnlyDictionary<string, string> stored, string key, bool defaultValue)
    {
        if (!stored.TryGetValue(key, out var raw)) return defaultValue;
        try { return JsonSerializer.Deserialize<bool>(raw); }
        catch { return bool.TryParse(raw.Trim('"'), out var b) && b; }
    }

    private static int ReadJsonInt(IReadOnlyDictionary<string, string> stored, string key, int defaultValue)
    {
        if (!stored.TryGetValue(key, out var raw)) return defaultValue;
        try { return JsonSerializer.Deserialize<int>(raw); }
        catch { return int.TryParse(raw.Trim('"'), out var n) ? n : defaultValue; }
    }

    public async Task UpdateDashboardTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        await _config.SetAsync(KeyDashboardToken, JsonSerializer.Serialize(token), cancellationToken);
        _kanbanSync.SetDashboardToken(string.IsNullOrWhiteSpace(token) ? null : token);
    }

    public async Task BootstrapKanbanSyncAsync(CancellationToken cancellationToken = default)
    {
        var tokenJson = await _config.GetAsync(KeyDashboardToken, cancellationToken);
        if (tokenJson is null) return;

        try
        {
            var token = JsonSerializer.Deserialize<string>(tokenJson);
            _kanbanSync.SetDashboardToken(token);
        }
        catch
        {
            _kanbanSync.SetDashboardToken(tokenJson.Trim('"'));
        }
    }
}

public record AppSettingsDto(
    string InstallRoot,
    string ApiBaseUrl,
    string DashboardBaseUrl,
    string Profile,
    string DashboardSessionToken,
    bool DashboardReachable,
    bool AutoSyncEnabled = false,
    int AutoSyncIntervalSeconds = 120);
