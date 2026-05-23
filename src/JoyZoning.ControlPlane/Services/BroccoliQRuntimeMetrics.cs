namespace JoyZoning.ControlPlane.Services;

/// <summary>Thread-safe counters for BroccoliQ mirroring (observability / health).</summary>
public sealed class BroccoliQRuntimeMetrics
{
    private long _enqueued;
    private long _delivered;
    private long _dropped;
    private long _failed;
    private long _tasksMirrored;
    private int _queueDepth;
    private string? _lastError;
    private DateTimeOffset? _lastDeliveredAt;

    public void RecordEnqueued() => Interlocked.Increment(ref _enqueued);

    public void RecordDropped() => Interlocked.Increment(ref _dropped);

    public void RecordDelivered(int count)
    {
        Interlocked.Add(ref _delivered, count);
        _lastDeliveredAt = DateTimeOffset.UtcNow;
    }

    public void RecordFailed(string? error)
    {
        Interlocked.Increment(ref _failed);
        if (!string.IsNullOrWhiteSpace(error))
            _lastError = error.Length > 500 ? error[..500] : error;
    }

    public void RecordTasksMirrored() => Interlocked.Increment(ref _tasksMirrored);

    public void SetQueueDepth(int depth) => Interlocked.Exchange(ref _queueDepth, depth);

    public BroccoliQMirrorStats Snapshot() => new(
        Enqueued: Interlocked.Read(ref _enqueued),
        Delivered: Interlocked.Read(ref _delivered),
        Dropped: Interlocked.Read(ref _dropped),
        Failed: Interlocked.Read(ref _failed),
        TasksMirrored: Interlocked.Read(ref _tasksMirrored),
        QueueDepth: Volatile.Read(ref _queueDepth),
        LastError: _lastError,
        LastDeliveredAt: _lastDeliveredAt);
}

public record BroccoliQMirrorStats(
    long Enqueued,
    long Delivered,
    long Dropped,
    long Failed,
    long TasksMirrored,
    int QueueDepth,
    string? LastError,
    DateTimeOffset? LastDeliveredAt);
