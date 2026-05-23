using System.Text.Json;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

public sealed class BroccoliQBackfillService
{
    public const string ConfigKeyLastBackfillEventId = "broccoliq.last_backfill_event_id";

    private readonly IEventRepository _events;
    private readonly IOperatorSessionRepository _sessions;
    private readonly IWorkTaskRepository _tasks;
    private readonly IConfigRepository _config;
    private readonly IBroccoliQBridge _bridge;
    private readonly BroccoliQCoordinator _coordinator;
    private readonly BroccoliQOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<BroccoliQBackfillService> _logger;

    public BroccoliQBackfillService(
        IEventRepository events,
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IConfigRepository config,
        IBroccoliQBridge bridge,
        BroccoliQCoordinator coordinator,
        IOptions<BroccoliQOptions> options,
        IHostEnvironment environment,
        ILogger<BroccoliQBackfillService> logger)
    {
        _events = events;
        _sessions = sessions;
        _tasks = tasks;
        _config = config;
        _bridge = bridge;
        _coordinator = coordinator;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<BroccoliQBackfillResult> RunAsync(
        int? maxEvents = null,
        bool includeTasks = true,
        CancellationToken cancellationToken = default)
    {
        if (!_bridge.IsEnabled || _environment.IsEnvironment("Testing"))
            return new BroccoliQBackfillResult(0, 0, Skipped: true, "disabled");

        var health = await _bridge.GetHealthAsync(cancellationToken);
        if (health.State != Domain.Enums.HealthState.Healthy)
            return new BroccoliQBackfillResult(0, 0, Skipped: true, health.Message);

        var cap = Math.Clamp(maxEvents ?? _options.BackfillMaxEvents, 1, 50_000);
        var sinceId = await ReadLastBackfillCursorAsync(cancellationToken);
        var mirroredEvents = 0;
        var mirroredTasks = 0;

        while (mirroredEvents < cap && !cancellationToken.IsCancellationRequested)
        {
            var batchSize = Math.Min(_options.MirrorBatchSize, cap - mirroredEvents);
            var batch = await _events.QueryAsync(
                sinceCursor: sinceId > 0 ? sinceId : null,
                limit: batchSize,
                cancellationToken: cancellationToken);

            if (batch.Count == 0)
                break;

            var sent = await _bridge.BackfillJoyEventsAsync(batch, cancellationToken);
            mirroredEvents += sent;
            sinceId = batch[^1].Id;
            _coordinator.LastBackfillEventId = sinceId;

            if (batch.Count < batchSize)
                break;
        }

        await _config.SetAsync(
            ConfigKeyLastBackfillEventId,
            JsonSerializer.Serialize(sinceId),
            cancellationToken);

        if (includeTasks && _options.BackfillTasksOnStartup)
            mirroredTasks = await BackfillTasksAsync(cancellationToken);

        _logger.LogInformation(
            "BroccoliQ backfill complete: {Events} events, {Tasks} tasks (cursor {Cursor})",
            mirroredEvents,
            mirroredTasks,
            sinceId);

        return new BroccoliQBackfillResult(mirroredEvents, mirroredTasks, Skipped: false, null);
    }

    private async Task<int> BackfillTasksAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        var seen = new HashSet<Guid>();
        var sessions = WorkspaceSessionCatalog.SelectCanonicalSessions(
            await _sessions.ListAsync(cancellationToken));
        foreach (var session in sessions)
        {
            var tasks = await _tasks.ListByWorkspaceRootAsync(session.WorkspaceRoot, cancellationToken);
            foreach (var task in tasks)
            {
                if (!seen.Add(task.Id))
                    continue;

                await _bridge.MirrorWorkTaskAsync(
                    task.Id,
                    task.Title,
                    task.Description,
                    task.Status.ToString(),
                    (int)task.Risk,
                    cancellationToken);
                count++;
            }
        }

        return count;
    }

    private async Task<long> ReadLastBackfillCursorAsync(CancellationToken cancellationToken)
    {
        if (_coordinator.LastBackfillEventId > 0)
            return _coordinator.LastBackfillEventId;

        var stored = await _config.GetAsync(ConfigKeyLastBackfillEventId, cancellationToken);
        if (long.TryParse(stored?.Trim('"'), out var parsed) && parsed > 0)
        {
            _coordinator.LastBackfillEventId = parsed;
            return parsed;
        }

        return 0;
    }
}

public record BroccoliQBackfillResult(
    int EventsMirrored,
    int TasksMirrored,
    bool Skipped,
    string? Reason);
