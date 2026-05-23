using System.Collections.Concurrent;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Prevents two active leases from mirroring into the same live folder.</summary>
public sealed class WorkspaceLiveMirrorRegistry
{
    private readonly ConcurrentDictionary<string, MirrorRegistration> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, MirrorObservabilityOutcome> _outcomes = new();

    public bool TryAcquire(string mirrorRoot, Guid leaseId, Guid taskId, Guid mirrorKeyId)
    {
        var registration = new MirrorRegistration(leaseId, taskId, mirrorKeyId, DateTimeOffset.UtcNow, mirrorRoot);
        var added = _active.TryAdd(mirrorRoot, registration);
        if (added)
            return true;

        if (_active.TryGetValue(mirrorRoot, out var existing) && existing.LeaseId == leaseId)
            return true;

        return false;
    }

    public void Release(string mirrorRoot, Guid leaseId)
    {
        if (_active.TryGetValue(mirrorRoot, out var existing) && existing.LeaseId == leaseId)
            _active.TryRemove(mirrorRoot, out _);
    }

    public void ReleaseForLease(Guid leaseId, string sessionRoot)
    {
        foreach (var kv in _active)
        {
            if (kv.Value.LeaseId != leaseId)
                continue;

            if (kv.Key.StartsWith(sessionRoot, StringComparison.OrdinalIgnoreCase)
                || WorkspacePaths.IsSameOrChildWorkspace(kv.Key, sessionRoot))
                _active.TryRemove(kv.Key, out _);
        }
    }

    public bool HasCollision(string mirrorRoot, Guid leaseId) =>
        _active.TryGetValue(mirrorRoot, out var existing) && existing.LeaseId != leaseId;

    public MirrorRegistration? GetOccupant(string mirrorRoot) =>
        _active.TryGetValue(mirrorRoot, out var existing) ? existing : null;

    public void RecordOutcome(
        Guid leaseId,
        LiveMirrorHealthState health,
        string? detail = null,
        string? mirrorRoot = null,
        Guid? occupyingLeaseId = null)
    {
        _outcomes[leaseId] = new MirrorObservabilityOutcome(
            health,
            detail,
            mirrorRoot,
            occupyingLeaseId,
            DateTimeOffset.UtcNow);
    }

    public MirrorObservabilityOutcome? GetOutcome(Guid leaseId) =>
        _outcomes.TryGetValue(leaseId, out var outcome) ? outcome : null;

    public IReadOnlyList<MirrorObservabilityOutcome> ListOutcomes() =>
        _outcomes.Values.ToList();

    public IReadOnlyList<MirrorRegistration> ListActive() =>
        _active.Values.ToList();

    public sealed record MirrorObservabilityOutcome(
        LiveMirrorHealthState Health,
        string? Detail,
        string? MirrorRoot,
        Guid? OccupyingLeaseId,
        DateTimeOffset RecordedAt);

    public sealed record MirrorRegistration(
        Guid LeaseId,
        Guid TaskId,
        Guid MirrorKeyId,
        DateTimeOffset AcquiredAt,
        string MirrorRoot);
}
