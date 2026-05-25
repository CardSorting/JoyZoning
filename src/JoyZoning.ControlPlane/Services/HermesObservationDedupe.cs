using System.Collections.Concurrent;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// In-memory idempotency for Hermes observation ingest (short TTL window).
/// </summary>
public class HermesObservationDedupe
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    public bool TryAccept(string dedupeKey, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(dedupeKey))
            return true;

        Prune();
        var now = DateTimeOffset.UtcNow;
        if (_seen.TryGetValue(dedupeKey, out var prior) && now - prior < Ttl)
        {
            reason = "duplicate_observation";
            return false;
        }

        _seen[dedupeKey] = now;
        return true;
    }

    private void Prune()
    {
        var cutoff = DateTimeOffset.UtcNow - Ttl;
        foreach (var kv in _seen)
        {
            if (kv.Value < cutoff)
                _seen.TryRemove(kv.Key, out _);
        }
    }
}
