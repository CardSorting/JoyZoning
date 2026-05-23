using JoyZoning.ControlPlane.Services;
using Xunit;

namespace JoyZoning.Tests;

[Trait("Category", "Unit")]
public class BroccoliQRuntimeMetricsTests
{
    [Fact]
    public void Snapshot_reflects_recorded_counters()
    {
        var metrics = new BroccoliQRuntimeMetrics();
        metrics.RecordEnqueued();
        metrics.RecordEnqueued();
        metrics.RecordDelivered(3);
        metrics.RecordDropped();
        metrics.RecordFailed("timeout");
        metrics.RecordTasksMirrored();
        metrics.SetQueueDepth(7);

        var snap = metrics.Snapshot();

        Assert.Equal(2, snap.Enqueued);
        Assert.Equal(3, snap.Delivered);
        Assert.Equal(1, snap.Dropped);
        Assert.Equal(1, snap.Failed);
        Assert.Equal(1, snap.TasksMirrored);
        Assert.Equal(7, snap.QueueDepth);
        Assert.Equal("timeout", snap.LastError);
        Assert.NotNull(snap.LastDeliveredAt);
    }
}
