using System.Collections.Concurrent;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Simple per-key rate limit for observation ingest (default: 120 events / minute).
/// </summary>
public class HermesObservationRateLimiter
{
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _buckets = new();
    private const int MaxPerMinute = 120;

    public bool Allow(string key, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(key))
            key = "global";

        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-1);
        var list = _buckets.GetOrAdd(key, _ => new List<DateTimeOffset>());
        lock (list)
        {
            list.RemoveAll(t => t < windowStart);
            if (list.Count >= MaxPerMinute)
            {
                reason = "rate_limited";
                return false;
            }

            list.Add(now);
            return true;
        }
    }
}
