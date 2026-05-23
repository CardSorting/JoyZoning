using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Background;

/// <summary>Periodic git/mtime scan of active lease worktrees (complements on-demand API refresh).</summary>
public class LeaseWorktreeMonitorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<LeaseRuntimeOptions> _leaseOptions;
    private readonly IOptions<WorkspaceOptions> _workspaceOptions;
    private readonly ILogger<LeaseWorktreeMonitorHostedService> _logger;

    public LeaseWorktreeMonitorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<LeaseRuntimeOptions> leaseOptions,
        IOptions<WorkspaceOptions> workspaceOptions,
        ILogger<LeaseWorktreeMonitorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _leaseOptions = leaseOptions;
        _workspaceOptions = workspaceOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_leaseOptions.Value.WorktreeMonitorEnabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var monitor = scope.ServiceProvider.GetRequiredService<LeaseWorktreeMonitor>();
                    var published = await monitor.ScanActiveLeasesAsync(stoppingToken);
                    if (published > 0)
                    {
                        _logger.LogDebug(
                            "Worktree monitor published {Count} lease snapshot(s)",
                            published);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "Worktree monitor tick failed");
                }
            }

            var interval = Math.Max(
                5,
                _workspaceOptions.Value.LiveMonitorIntervalSeconds > 0
                    ? _workspaceOptions.Value.LiveMonitorIntervalSeconds
                    : _leaseOptions.Value.WorktreeMonitorIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }
}
