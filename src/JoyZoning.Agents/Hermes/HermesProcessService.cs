using System.Diagnostics;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JoyZoning.Agents.Hermes;

public class HermesProcessService
{
    private readonly HermesRuntimeSettings _settings;
    private readonly HermesHttpClient _client;
    private readonly ILogger<HermesProcessService> _logger;
    private Process? _gatewayProcess;

    public HermesProcessService(
        HermesRuntimeSettings settings,
        HermesHttpClient client,
        ILogger<HermesProcessService> logger)
    {
        _settings = settings;
        _client = client;
        _logger = logger;
    }

    public async Task EnsureGatewayRunningAsync(CancellationToken cancellationToken = default)
    {
        var health = await _client.GetHealthAsync(cancellationToken);
        if (health.State == HealthState.Healthy)
            return;

        if (!_settings.AutoStartGateway)
        {
            _logger.LogWarning("Hermes gateway not reachable and AutoStartGateway is disabled");
            return;
        }

        await StartGatewayAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

        health = await _client.GetHealthAsync(cancellationToken);
        if (health.State != HealthState.Healthy)
            _logger.LogWarning("Hermes gateway may not be ready: {Message}", health.Message);
    }

    public Task StartGatewayAsync(CancellationToken cancellationToken = default)
    {
        if (_gatewayProcess is { HasExited: false })
            return Task.CompletedTask;

        var hermesCmd = HermesCliLocator.FindHermesExecutable(_settings.InstallRoot);
        if (hermesCmd is null)
        {
            _logger.LogWarning("Could not find hermes executable in PATH or install root");
            return Task.CompletedTask;
        }

        var args = string.IsNullOrEmpty(_settings.Profile)
            ? "gateway"
            : $"-p {_settings.Profile} gateway";

        var psi = new ProcessStartInfo
        {
            FileName = hermesCmd,
            Arguments = args,
            WorkingDirectory = _settings.InstallRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        psi.Environment["API_SERVER_ENABLED"] = "1";

        _gatewayProcess = Process.Start(psi);
        _logger.LogInformation("Started Hermes gateway: {File} {Args}", hermesCmd, args);
        return Task.CompletedTask;
    }

}
