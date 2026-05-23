using JoyZoning.Agents.Hermes;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Orchestration;
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
                _logger.LogWarning(ex, "Kanban auto-sync tick failed");
                _state.LastMessage = ex.Message;
                _state.LastOutcome = KanbanSyncOutcome.Error;
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

        if (!settings.AutoSyncEnabled)
        {
            _state.LastMessage = "Auto-sync disabled";
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.DashboardSessionToken))
        {
            _state.LastMessage = "Auto-sync waiting for dashboard token — run hermes ensure-dashboard";
            return;
        }

        if (!settings.DashboardReachable)
        {
            _state.LastMessage = "Auto-sync waiting for dashboard reachability";
            return;
        }

        var sessions = scope.ServiceProvider.GetRequiredService<IOperatorSessionRepository>();
        var orch = scope.ServiceProvider.GetRequiredService<OrchestrationService>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OperatorHub>>();

        var all = await sessions.ListAsync(cancellationToken);
        if (all.Count == 0)
        {
            _state.LastMessage = "No sessions to sync";
            return;
        }

        var failures = 0;
        var syncedWorkspaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var syncedCount = 0;
        foreach (var session in all)
        {
            var workspaceKey = WorkspacePaths.TryNormalize(session.WorkspaceRoot, out var norm)
                ? norm
                : session.WorkspaceRoot;
            if (!syncedWorkspaces.Add(workspaceKey))
                continue;

            try
            {
                syncedCount++;
                var result = await orch.SyncKanbanTwoWayAsync(session.Id, cancellationToken);
                await hub.Clients.All.SendAsync(
                    "OnKanbanSynced",
                    new { sessionId = session.Id, message = result.Message },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                failures++;
                _logger.LogWarning(ex, "Kanban sync failed for session {SessionId}", session.Id);
            }
        }

        _state.LastSyncAt = DateTimeOffset.UtcNow;
        _state.LastMessage = failures == 0
            ? $"Auto-sync OK ({syncedCount} workspace(s), {all.Count} session(s))"
            : $"Auto-sync partial: {failures}/{syncedCount} workspace(s) failed — see logs";
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
