using JoyZoning.ControlPlane.Hubs;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace JoyZoning.ControlPlane.Background;

public class KanbanAutoSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KanbanSyncState _state;
    private readonly ILogger<KanbanAutoSyncHostedService> _logger;

    public KanbanAutoSyncHostedService(
        IServiceScopeFactory scopeFactory,
        KanbanSyncState state,
        ILogger<KanbanAutoSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Kanban auto-sync tick failed");
                _state.LastMessage = ex.Message;
            }

            var delay = await GetIntervalAsync(stoppingToken);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<ConfigService>();
        var settings = await config.GetSettingsAsync(cancellationToken);

        if (!settings.AutoSyncEnabled
            || string.IsNullOrWhiteSpace(settings.DashboardSessionToken)
            || !settings.DashboardReachable)
        {
            _state.LastMessage = settings.AutoSyncEnabled
                ? "Auto-sync waiting for dashboard token / reachability"
                : "Auto-sync disabled";
            return;
        }

        var sessions = scope.ServiceProvider.GetRequiredService<IOperatorSessionRepository>();
        var orch = scope.ServiceProvider.GetRequiredService<OrchestrationService>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OperatorHub>>();

        var all = await sessions.ListAsync(cancellationToken);
        foreach (var session in all)
        {
            var result = await orch.SyncKanbanTwoWayAsync(session.Id, cancellationToken);
            await hub.Clients.All.SendAsync(
                "OnKanbanSynced",
                new { sessionId = session.Id, message = result.Message },
                cancellationToken);
        }

        if (all.Count == 0)
            _state.LastMessage = "No sessions to sync";
    }

    private async Task<TimeSpan> GetIntervalAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<ConfigService>();
        var settings = await config.GetSettingsAsync(cancellationToken);
        var seconds = Math.Clamp(settings.AutoSyncIntervalSeconds, 30, 3600);
        return TimeSpan.FromSeconds(seconds);
    }
}

public class KanbanSyncState
{
    public DateTimeOffset? LastSyncAt { get; set; }
    public string LastMessage { get; set; } = "Not synced yet";
}
