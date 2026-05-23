using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Background;

/// <summary>Starts the joy-bridge worker, runs startup backfill, and supervises restarts.</summary>
public sealed class BroccoliQWorkerHostedService : BackgroundService
{
    private readonly BroccoliQProcessService _process;
    private readonly IBroccoliQBridge _bridge;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BroccoliQOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<BroccoliQWorkerHostedService> _logger;

    public BroccoliQWorkerHostedService(
        BroccoliQProcessService process,
        IBroccoliQBridge bridge,
        IServiceScopeFactory scopeFactory,
        IOptions<BroccoliQOptions> options,
        IHostEnvironment environment,
        ILogger<BroccoliQWorkerHostedService> logger)
    {
        _process = process;
        _bridge = bridge;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.AutoStartWorker || _environment.IsEnvironment("Testing"))
            return;

        await EnsureOnceAsync(stoppingToken);

        if (_options.BackfillOnStartup)
            await RunBackfillAsync(stoppingToken);

        if (!_options.SupervisorEnabled)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutdown
            }

            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.WorkerSupervisorIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (!_bridge.IsEnabled)
                    continue;

                var health = await _bridge.GetHealthAsync(stoppingToken);
                if (health.State == HealthState.Healthy)
                    continue;

                _logger.LogInformation("BroccoliQ bridge unhealthy ({Message}); restarting worker", health.Message);
                var restarted = await _process.EnsureWorkerRunningAsync(stoppingToken);
                if (restarted && _options.BackfillOnStartup)
                    await RunBackfillAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
    }

    private async Task EnsureOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _process.EnsureWorkerRunningAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BroccoliQ worker startup failed; event mirroring may be unavailable");
        }
    }

    private async Task RunBackfillAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<BroccoliQBackfillService>();
            var result = await backfill.RunAsync(cancellationToken: stoppingToken);
            if (!result.Skipped && (result.EventsMirrored > 0 || result.TasksMirrored > 0))
            {
                _logger.LogInformation(
                    "BroccoliQ startup backfill: {Events} events, {Tasks} tasks",
                    result.EventsMirrored,
                    result.TasksMirrored);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BroccoliQ startup backfill failed");
        }
    }
}
