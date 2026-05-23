using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

public class HermesConnectivityService
{
    private readonly HermesHttpClient _client;
    private readonly HermesProcessService _process;
    private readonly HermesRuntimeSettings _settings;
    private readonly IHostEnvironment _environment;

    public HermesConnectivityService(
        HermesHttpClient client,
        HermesProcessService process,
        HermesRuntimeSettings settings,
        IHostEnvironment environment)
    {
        _client = client;
        _process = process;
        _settings = settings;
        _environment = environment;
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

        var snapshot = _settings.GetSnapshot();
        if (string.IsNullOrWhiteSpace(snapshot.ApiKey))
        {
            throw new InvalidOperationException(
                $"Hermes API key missing for profile '{snapshot.Profile}'. " +
                "Set API_SERVER_KEY in the profile .env or Hermes:ApiKey in appsettings.");
        }

        var report = await EnsureAsync(cancellationToken);
        if (report.State == HealthState.Healthy)
            return;

        throw new InvalidOperationException(report.Message);
    }
}

public record HermesHealthReport(
    HealthState State,
    string Message,
    bool AutoStartEnabled);
