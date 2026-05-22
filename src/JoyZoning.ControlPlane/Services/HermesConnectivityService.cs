using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Enums;

namespace JoyZoning.ControlPlane.Services;

public class HermesConnectivityService
{
    private readonly HermesHttpClient _client;
    private readonly HermesProcessService _process;

    public HermesConnectivityService(HermesHttpClient client, HermesProcessService process)
    {
        _client = client;
        _process = process;
    }

    public async Task<HermesHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = await _client.GetHealthAsync(cancellationToken);
        return new HermesHealthReport(
            health.State,
            health.Message,
            AutoStartEnabled: true);
    }

    public async Task<HermesHealthReport> EnsureAsync(CancellationToken cancellationToken = default)
    {
        await _process.EnsureGatewayRunningAsync(cancellationToken);
        return await GetHealthAsync(cancellationToken);
    }
}

public record HermesHealthReport(
    HealthState State,
    string Message,
    bool AutoStartEnabled);
