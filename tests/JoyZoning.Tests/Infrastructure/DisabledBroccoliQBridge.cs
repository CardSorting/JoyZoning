using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Tests.Infrastructure;

internal sealed class DisabledBroccoliQBridge : IBroccoliQBridge
{
    public bool IsEnabled => false;

    public BroccoliQMirrorStats GetMirrorStats() => new(0, 0, 0, 0, 0, 0, null, null);

    public Task<BroccoliQHealthReport> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new BroccoliQHealthReport(
            HealthState.Unavailable,
            "disabled in unit tests",
            Enabled: false,
            WorkerAutoStart: false));

    public void EnqueueJoyEvent(JoyEvent joyEvent) { }

    public Task<int> BackfillJoyEventsAsync(
        IReadOnlyList<JoyEvent> events,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task FlushBridgeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MirrorWorkTaskAsync(
        Guid taskId,
        string title,
        string? description,
        string status,
        int priority = 0,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<BroccoliQHiveAuditPage> QueryHiveAuditAsync(
        int limit = 100,
        string typePrefix = "joy.",
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new BroccoliQHiveAuditPage(0, Array.Empty<BroccoliQHiveAuditRow>()));

    public Task<BroccoliQHiveTaskPage> QueryHiveTasksAsync(
        int limit = 100,
        Guid? taskId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new BroccoliQHiveTaskPage(0, Array.Empty<BroccoliQHiveTaskRow>()));
}
