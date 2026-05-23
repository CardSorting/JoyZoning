using System.Diagnostics;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Agents.Hermes;

public class HermesProcessService : IDisposable
{
    private readonly HermesRuntimeSettings _settings;
    private readonly HermesHttpClient _client;
    private readonly ILogger<HermesProcessService> _logger;
    private readonly object _processLock = new();
    private Process? _gatewayProcess;

    public HermesProcessService(
        HermesRuntimeSettings settings,
        HermesHttpClient client,
        ILogger<HermesProcessService> logger,
        IHostApplicationLifetime? lifetime = null)
    {
        _settings = settings;
        _client = client;
        _logger = logger;
        lifetime?.ApplicationStopping.Register(StopGatewayProcess);
    }

    public void Dispose() => StopGatewayProcess();

    private void StopGatewayProcess()
    {
        lock (_processLock)
        {
            if (_gatewayProcess is not { HasExited: false })
                return;
            try
            {
                _gatewayProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to stop Hermes gateway process on shutdown");
            }
            finally
            {
                _gatewayProcess.Dispose();
                _gatewayProcess = null;
            }
        }
    }

    /// <summary>Stop any JoyZoning-managed gateway and start a fresh process with current profile credentials.</summary>
    public async Task<bool> RestartGatewayAsync(CancellationToken cancellationToken = default)
    {
        StopGatewayProcess();
        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
        await StartGatewayAsync(cancellationToken);
        return await WaitForHealthyAsync(maxWait: TimeSpan.FromSeconds(45), cancellationToken);
    }

    public async Task<bool> EnsureGatewayRunningAsync(CancellationToken cancellationToken = default)
    {
        if ((await _client.GetHealthAsync(cancellationToken)).State == HealthState.Healthy)
            return true;

        var snapshot = _settings.GetSnapshot();
        if (!snapshot.AutoStartGateway)
        {
            _logger.LogWarning("Hermes gateway not reachable and AutoStartGateway is disabled");
            return false;
        }

        await StartGatewayAsync(cancellationToken);
        return await WaitForHealthyAsync(maxWait: TimeSpan.FromSeconds(45), cancellationToken);
    }

    public Task StartGatewayAsync(CancellationToken cancellationToken = default)
    {
        lock (_processLock)
        {
            if (_gatewayProcess is { HasExited: false })
                return Task.CompletedTask;

            if (_gatewayProcess is { HasExited: true })
            {
                try { _gatewayProcess.Dispose(); } catch { /* ignore */ }
                _gatewayProcess = null;
            }

            var snapshot = _settings.GetSnapshot();
            var hermesCmd = HermesCliLocator.FindHermesExecutable(snapshot.InstallRoot);
            if (hermesCmd is null)
            {
                _logger.LogWarning("Could not find hermes executable in PATH or install root {Root}", snapshot.InstallRoot);
                return Task.CompletedTask;
            }

            var args = string.IsNullOrEmpty(snapshot.Profile)
                ? "gateway"
                : $"-p {snapshot.Profile} gateway";

            var psi = new ProcessStartInfo
            {
                FileName = hermesCmd,
                Arguments = args,
                WorkingDirectory = snapshot.InstallRoot,
            };
            psi.Environment["API_SERVER_ENABLED"] = "1";
            var apiKey = snapshot.ApiKey
                ?? HermesProfileEnv.TryReadApiServerKey(snapshot.Profile);
            if (!string.IsNullOrWhiteSpace(apiKey))
                psi.Environment["API_SERVER_KEY"] = apiKey;

            _gatewayProcess = HermesChildProcessHost.Start(psi, _logger, "hermes-gateway");
            if (_gatewayProcess is null)
            {
                _logger.LogWarning("Failed to start Hermes gateway process");
                return Task.CompletedTask;
            }

            _logger.LogInformation("Started Hermes gateway: {File} {Args}", hermesCmd, args);
        }

        return Task.CompletedTask;
    }

    private async Task<bool> WaitForHealthyAsync(TimeSpan maxWait, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + maxWait;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((await _client.GetHealthAsync(cancellationToken)).State == HealthState.Healthy)
                return true;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        var health = await _client.GetHealthAsync(cancellationToken);
        _logger.LogWarning("Hermes gateway not ready after {Seconds}s: {Message}", maxWait.TotalSeconds, health.Message);
        return health.State == HealthState.Healthy;
    }
}
