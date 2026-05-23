using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.ControlPlane.Services;

public interface IBroccoliQBridge
{
    bool IsEnabled { get; }

    BroccoliQMirrorStats GetMirrorStats();

    Task<BroccoliQHealthReport> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Non-blocking enqueue; drops when the mirror queue is full (never blocks ingest).</summary>
    void EnqueueJoyEvent(JoyEvent joyEvent);

    /// <summary>Direct batch mirror for backfill (bypasses the live queue).</summary>
    Task<int> BackfillJoyEventsAsync(
        IReadOnlyList<JoyEvent> events,
        CancellationToken cancellationToken = default);

    Task FlushBridgeAsync(CancellationToken cancellationToken = default);

    Task MirrorWorkTaskAsync(
        Guid taskId,
        string title,
        string? description,
        string status,
        int priority = 0,
        CancellationToken cancellationToken = default);
}

public record BroccoliQHealthReport(
    HealthState State,
    string Message,
    bool Enabled,
    bool WorkerAutoStart,
    string? DatabasePath = null,
    bool? BridgeBuilt = null,
    int? BridgeMirrorCount = null);
