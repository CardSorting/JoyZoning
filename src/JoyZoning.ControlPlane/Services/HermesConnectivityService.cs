using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

public class HermesConnectivityService
{
    private readonly HermesHttpClient _client;
    private readonly HermesProcessService _process;
    private readonly HermesRuntimeSettings _settings;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<HermesConnectivityService> _logger;

    public HermesConnectivityService(
        HermesHttpClient client,
        HermesProcessService process,
        HermesRuntimeSettings settings,
        IHostEnvironment environment,
        ILogger<HermesConnectivityService> logger)
    {
        _client = client;
        _process = process;
        _settings = settings;
        _environment = environment;
        _logger = logger;
    }

    public async Task<HermesHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = await _client.GetHealthAsync(cancellationToken);
        return new HermesHealthReport(
            health.State,
            health.Message,
            _settings.GetSnapshot().AutoStartGateway);
    }

    public async Task<HermesHealthReport> EnsureAsync(CancellationToken cancellationToken = default)
    {
        var ready = await _process.EnsureGatewayRunningAsync(cancellationToken);
        var health = await GetHealthAsync(cancellationToken);
        if (!ready && health.State != HealthState.Healthy)
        {
            return health with
            {
                Message = $"Hermes API not ready: {health.Message}",
                State = HealthState.Unavailable,
            };
        }

        return health;
    }

    public async Task EnsureReadyForAgentCallsAsync(CancellationToken cancellationToken = default)
    {
        if (_environment.IsEnvironment("Testing"))
            return;

        _settings.ReloadApiKeyFromProfile();
        _client.RefreshConnection();

        var snapshot = _settings.GetSnapshot();
        if (string.IsNullOrWhiteSpace(snapshot.ApiKey))
        {
            throw new InvalidOperationException(
                $"Hermes API key missing for profile '{snapshot.Profile}'. " +
                "Set API_SERVER_KEY in ~/.hermes/profiles/joyzoning/.env or run ./scripts/install-diet-hermes.sh.");
        }

        var report = await EnsureAsync(cancellationToken);
        if (report.State != HealthState.Healthy)
            throw new InvalidOperationException(report.Message);

        if (await _client.VerifyApiKeyAsync(cancellationToken))
            return;

        _logger.LogWarning(
            "Hermes API key rejected by gateway for profile '{Profile}' — restarting gateway with profile credentials",
            snapshot.Profile);

        if (!await _process.RestartGatewayAsync(cancellationToken))
            throw new InvalidOperationException(
                "Hermes gateway is reachable but rejected the API key. " +
                "Stop any manual `hermes gateway` process and run: curl -X POST http://127.0.0.1:9470/api/hermes/sync-credentials");

        _settings.ReloadApiKeyFromProfile();
        _client.RefreshConnection();

        if (!await _client.VerifyApiKeyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Hermes gateway still rejects the API key after restart. " +
                "Regenerate API_SERVER_KEY in ~/.hermes/profiles/joyzoning/.env, then POST /api/hermes/sync-credentials.");
        }
    }

    /// <summary>Reload profile API key and restart the gateway so it matches JoyZoning.</summary>
    public async Task<HermesCredentialSyncReport> SyncCredentialsAsync(CancellationToken cancellationToken = default)
    {
        _settings.ReloadApiKeyFromProfile();
        _client.RefreshConnection();

        var snapshot = _settings.GetSnapshot();
        if (string.IsNullOrWhiteSpace(snapshot.ApiKey))
        {
            return new HermesCredentialSyncReport(
                false,
                $"No API_SERVER_KEY in profile '{snapshot.Profile}'. Run ./scripts/install-diet-hermes.sh.");
        }

        var restarted = await _process.RestartGatewayAsync(cancellationToken);
        if (!restarted)
        {
            return new HermesCredentialSyncReport(
                false,
                "Could not restart Hermes gateway. Stop any process on port 8642 and retry.");
        }

        _settings.ReloadApiKeyFromProfile();
        _client.RefreshConnection();

        var ok = await _client.VerifyApiKeyAsync(cancellationToken);
        return new HermesCredentialSyncReport(
            ok,
            ok
                ? $"API key accepted for profile '{snapshot.Profile}'."
                : "Gateway restarted but still rejects the API key — check ~/.hermes/profiles/joyzoning/.env.");
    }
}

public record HermesCredentialSyncReport(bool Ok, string Message);

public record HermesHealthReport(
    HealthState State,
    string Message,
    bool AutoStartEnabled);
