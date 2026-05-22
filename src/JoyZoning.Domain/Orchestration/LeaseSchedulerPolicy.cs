using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Deterministic lease duration and stale detection (no wall-clock randomness beyond UTC now).</summary>
public static class LeaseSchedulerPolicy
{
    public static DateTimeOffset ComputeExpiresAt(LeaseRiskLevel risk, DateTimeOffset startedAt, LeaseRuntimeOptions options)
    {
        var hours = risk switch
        {
            LeaseRiskLevel.Critical => options.Duration.CriticalHours,
            LeaseRiskLevel.Medium => options.Duration.MediumHours,
            _ => options.Duration.LowHours,
        };
        return startedAt.AddHours(hours);
    }

    public static TimeSpan HeartbeatStaleThreshold(ExecutionLease lease, LeaseRuntimeOptions options) =>
        lease.RiskLevel == LeaseRiskLevel.Critical
            ? lease.Status switch
            {
                ExecutionLeaseStatus.Leased => TimeSpan.FromMinutes(options.Stale.CriticalLeasedMinutes),
                ExecutionLeaseStatus.Running => TimeSpan.FromMinutes(options.Stale.CriticalRunningMinutes),
                ExecutionLeaseStatus.Verifying => TimeSpan.FromMinutes(options.Stale.CriticalVerifyingMinutes),
                _ => TimeSpan.FromHours(24),
            }
            : lease.Status switch
            {
                ExecutionLeaseStatus.Leased => TimeSpan.FromMinutes(options.Stale.LeasedMinutes),
                ExecutionLeaseStatus.Running => TimeSpan.FromMinutes(options.Stale.RunningMinutes),
                ExecutionLeaseStatus.Verifying => TimeSpan.FromMinutes(options.Stale.VerifyingMinutes),
                _ => TimeSpan.FromHours(24),
            };

    public static bool IsPastExpiresAt(ExecutionLease lease, DateTimeOffset now) =>
        now >= lease.ExpiresAt;

    public static bool IsHeartbeatStale(ExecutionLease lease, DateTimeOffset now, LeaseRuntimeOptions options)
    {
        if (!KanbanExecutionRules.ActiveLeaseStatuses.Contains(lease.Status))
            return false;

        if (lease.Status is ExecutionLeaseStatus.Blocked or ExecutionLeaseStatus.ReadyForReview)
            return false;

        var last = lease.LastHeartbeatAt ?? lease.StartedAt;
        return now - last > HeartbeatStaleThreshold(lease, options);
    }

    public static string DescribeStaleReason(ExecutionLease lease, DateTimeOffset now, LeaseRuntimeOptions options)
    {
        if (IsPastExpiresAt(lease, now))
            return $"Lease expired at {lease.ExpiresAt:O}.";

        var threshold = HeartbeatStaleThreshold(lease, options);
        var last = lease.LastHeartbeatAt ?? lease.StartedAt;
        return $"No heartbeat for {lease.Status} since {last:O} (threshold {threshold.TotalMinutes:F0}m).";
    }

    public static bool ShouldRevokeOnExpiration(LeaseRuntimeOptions options) =>
        options.AbsoluteExpirationRevokes;
}
