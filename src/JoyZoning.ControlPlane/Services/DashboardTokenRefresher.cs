using JoyZoning.Agents.Hermes;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Scoped bridge: scrape dashboard token and push it into <see cref="KanbanSyncService"/>.</summary>
public sealed class DashboardTokenRefresher : IDashboardTokenRefresher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DashboardTokenRefresher> _logger;

    public DashboardTokenRefresher(
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardTokenRefresher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dashboard = scope.ServiceProvider.GetRequiredService<HermesDashboardConnectivityService>();
            var result = await dashboard.RefreshTokenAsync(cancellationToken);
            if (result.TokenValid)
                return true;

            _logger.LogWarning("Dashboard token refresh completed but kanban validation failed: {Message}", result.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard token refresh failed");
            return false;
        }
    }
}
