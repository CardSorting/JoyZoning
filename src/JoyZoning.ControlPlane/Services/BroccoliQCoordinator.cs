namespace JoyZoning.ControlPlane.Services;

/// <summary>Cross-service BroccoliQ runtime flags (worker lifecycle + mirror pump gating).</summary>
public sealed class BroccoliQCoordinator
{
    private int _bridgeReady;
    private long _lastBackfillEventId;

    public bool BridgeReady => Volatile.Read(ref _bridgeReady) == 1;

    public void SetBridgeReady(bool ready) =>
        Volatile.Write(ref _bridgeReady, ready ? 1 : 0);

    public long LastBackfillEventId
    {
        get => Interlocked.Read(ref _lastBackfillEventId);
        set => Interlocked.Exchange(ref _lastBackfillEventId, value);
    }
}
