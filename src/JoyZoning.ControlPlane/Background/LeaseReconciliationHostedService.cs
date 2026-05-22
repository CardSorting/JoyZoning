namespace JoyZoning.ControlPlane.Background;

using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Configuration;
using Microsoft.Extensions.Options;

/// <summary>Periodic scheduler tick: stale leases, orphans, and invalid runtime combinations.</summary>
public class LeaseReconciliationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<LeaseRuntimeOptions> _options;
    private readonly ILogger<LeaseReconciliationHostedService> _logger;

    public LeaseReconciliationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<LeaseRuntimeOptions> options,
        ILogger<LeaseReconciliationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runtime = scope.ServiceProvider.GetRequiredService<LeaseRuntimeService>();
                var report = await runtime.ReconcileAsync(stoppingToken);
                if (report.StaleExpired + report.MissingWorktree + report.OrphanedRunning
                    + report.MergedTaskActiveLease + report.InvalidReadyForReview > 0)
                {
                    _logger.LogInformation(
                        "Lease reconciliation: stale={Stale} worktree={Wt} orphan={Orphan} merged={Merged} invalid={Invalid}",
                        report.StaleExpired,
                        report.MissingWorktree,
                        report.OrphanedRunning,
                        report.MergedTaskActiveLease,
                        report.InvalidReadyForReview);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Lease reconciliation tick failed");
            }

            var interval = Math.Max(15, _options.Value.ReconciliationIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }
}
