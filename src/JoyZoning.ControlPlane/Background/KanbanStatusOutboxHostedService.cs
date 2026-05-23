using JoyZoning.Agents.Hermes;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Configuration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Background;

public class KanbanStatusOutboxHostedService : BackgroundService
{
    private readonly KanbanStatusOutbox _outbox;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<WorkspaceParallelismOptions> _parallelism;
    private readonly ILogger<KanbanStatusOutboxHostedService> _logger;

    public KanbanStatusOutboxHostedService(
        KanbanStatusOutbox outbox,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkspaceParallelismOptions> parallelism,
        ILogger<KanbanStatusOutboxHostedService> logger)
    {
        _outbox = outbox;
        _scopeFactory = scopeFactory;
        _parallelism = parallelism;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var taskId in _outbox.Reader.ReadAllAsync(stoppingToken))
                    await DrainOneAsync(taskId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kanban status outbox drain failed");
                var pollMs = Math.Clamp(_parallelism.Value.KanbanOutboxPollIntervalMs, 50, 5000);
                await Task.Delay(TimeSpan.FromMilliseconds(pollMs), stoppingToken);
            }
        }
    }

    private async Task DrainOneAsync(Guid taskId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var tasks = scope.ServiceProvider.GetRequiredService<IWorkTaskRepository>();
        var kanban = scope.ServiceProvider.GetRequiredService<KanbanSyncService>();
        var config = scope.ServiceProvider.GetRequiredService<ConfigService>();

        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null || string.IsNullOrEmpty(task.HermesKanbanTaskId))
            return;

        if (task.KanbanRevision <= task.KanbanPushedRevision)
            return;

        var settings = await config.GetSettingsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.DashboardSessionToken))
            return;

        kanban.SetDashboardToken(settings.DashboardSessionToken);

        var revision = task.KanbanRevision;
        await kanban.SyncUpdateStatusAsync(task, cancellationToken);
        await tasks.MarkKanbanPushedAsync(taskId, revision, cancellationToken);
    }
}
