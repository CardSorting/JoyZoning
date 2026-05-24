using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public static class LeaseSchedulingRules
{
    public static string? ValidateCapacity(
        int globalActiveCount,
        int sessionActiveCount,
        ExecutionLease? globalCriticalOccupant,
        LeaseRiskLevel newLeaseRisk,
        bool humanApprovedCritical,
        LeaseRuntimeOptions options)
    {
        if (globalActiveCount >= options.MaxGlobalActiveLeases)
            return $"Scheduler at capacity ({options.MaxGlobalActiveLeases} global active leases).";

        if (sessionActiveCount >= options.MaxActiveLeasesPerSession)
            return options.MaxActiveLeasesPerSession <= 1
                ? "Single-agent session: another lease is already active. Finish or revoke before dispatching."
                : "Too many active leases for this operator session.";

        if (newLeaseRisk == LeaseRiskLevel.Critical)
        {
            if (!humanApprovedCritical)
                return "Critical card requires human approval before execution.";
            if (globalCriticalOccupant is not null)
                return "Another critical lease is already active globally.";
            if (options.MaxCriticalLeases < 1)
                return "Critical leases are disabled by scheduler policy.";
        }

        return null;
    }

    public static string? ValidateLeaseOwnership(
        ExecutionLease lease,
        Guid actingSessionId,
        StatusChangeActor actor,
        bool recoveryFlow = false)
    {
        if (recoveryFlow && actor is StatusChangeActor.Human or StatusChangeActor.System)
            return null;

        if (actor == StatusChangeActor.System)
            return null;

        if (lease.AssignedSessionId != actingSessionId)
            return "Active lease is owned by another operator session.";

        return null;
    }

    public static string? ValidateRecovery(
        ExecutionLease? lease,
        LeaseRecoveryMode mode,
        StatusChangeActor actor)
    {
        if (actor is not (StatusChangeActor.Human or StatusChangeActor.System))
            return "Only human or system actors may recover leases.";

        if (lease is null)
            return mode == LeaseRecoveryMode.ReplacementLease ? null : "No lease found to recover.";

        return mode switch
        {
            LeaseRecoveryMode.ReopenBlocked when lease.Status != ExecutionLeaseStatus.Blocked =>
                "ReopenBlocked requires a blocked lease.",
            LeaseRecoveryMode.ReattachWorktree when KanbanExecutionRules.IsTerminal(lease.Status) =>
                "Cannot reattach a terminal lease.",
            LeaseRecoveryMode.ReplacementLease => null,
            _ => null,
        };
    }

    public static string? ValidateDispatchRetry(ExecutionLease lease, bool humanApprovedCritical)
    {
        if (lease.Status != ExecutionLeaseStatus.Leased)
            return "Dispatch retry requires lease in leased status (recover blocked leases first).";

        if (lease.RiskLevel == LeaseRiskLevel.Critical && !humanApprovedCritical)
            return "Critical dispatch retry requires explicit human approval (consumed grants are not recycled).";

        return null;
    }
}
