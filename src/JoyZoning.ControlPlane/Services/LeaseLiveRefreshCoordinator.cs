using System.Collections.Concurrent;
using JoyZoning.ControlPlane.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Debounced live mirror refresh when DietCode emits tool/file events so the Watch UI
/// and session workspace see new code within ~1s instead of waiting for the background tick.
/// </summary>
public sealed class LeaseLiveRefreshCoordinator
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(350);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaseLiveRefreshCoordinator> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _debounce = new();

    public LeaseLiveRefreshCoordinator(
        IServiceScopeFactory scopeFactory,
        ILogger<LeaseLiveRefreshCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void RequestRefresh(Guid taskId)
    {
        if (taskId == Guid.Empty)
            return;

        var cts = new CancellationTokenSource();
        var prior = _debounce.AddOrUpdate(taskId, cts, (_, old) =>
        {
            try { old.Cancel(); } catch { /* ignore */ }
            old.Dispose();
            return cts;
        });

        if (!ReferenceEquals(prior, cts))
            cts.Dispose();

        _ = RunDebouncedAsync(taskId, cts);
    }

    private async Task RunDebouncedAsync(Guid taskId, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(Debounce, cts.Token);
            using var scope = _scopeFactory.CreateScope();
            var exec = scope.ServiceProvider.GetRequiredService<KanbanExecutionOrchestrator>();
            var mirror = scope.ServiceProvider.GetRequiredService<WorkspaceLiveMirrorService>();
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OperatorHub>>();

            var lease = await exec.GetActiveLeaseAsync(taskId, cts.Token);
            if (lease is null)
                return;

            var snapshot = await mirror.RefreshForLeaseAsync(lease, cts.Token);
            if (snapshot is null)
                return;

            await hub.Clients.All.SendAsync(
                "OnTaskLiveUpdated",
                new TaskLiveUpdatedDto(
                    taskId,
                    snapshot.LeaseStatus,
                    snapshot.Presentation.Headline,
                    snapshot.Presentation.ProgressPercent,
                    snapshot.FilesCopiedThisTick,
                    snapshot.UpdatedAt,
                    snapshot.LiveMirrorRoot,
                    snapshot.MirrorMode),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer refresh
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Fast live refresh failed for task {TaskId}", taskId);
        }
        finally
        {
            if (_debounce.TryRemove(taskId, out var current) && ReferenceEquals(current, cts))
                cts.Dispose();
        }
    }
}
