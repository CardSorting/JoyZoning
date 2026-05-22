using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Invariants for kanban card → DietCode execution leases.</summary>
public static class KanbanExecutionRules
{
    public static readonly ExecutionLeaseStatus[] ActiveLeaseStatuses =
    [
        ExecutionLeaseStatus.Leased,
        ExecutionLeaseStatus.Running,
        ExecutionLeaseStatus.Blocked,
        ExecutionLeaseStatus.Verifying,
        ExecutionLeaseStatus.ReadyForReview,
    ];

    public static readonly ExecutionLeaseStatus[] TerminalLeaseStatuses =
    [
        ExecutionLeaseStatus.Revoked,
        ExecutionLeaseStatus.Merged,
    ];

    public static readonly ExecutionLeaseStatus[] AgentAllowedLeaseTargets =
    [
        ExecutionLeaseStatus.Blocked,
        ExecutionLeaseStatus.Verifying,
    ];

    public static bool IsTerminal(ExecutionLeaseStatus status) =>
        TerminalLeaseStatuses.Contains(status);

    public static bool IsActiveLease(ExecutionLease? lease) =>
        lease is not null && ActiveLeaseStatuses.Contains(lease.Status);

    public static bool IsCriticalLease(ExecutionLease lease) =>
        lease.RiskLevel == LeaseRiskLevel.Critical;

    public static bool OccupiesGlobalCriticalSlot(ExecutionLease lease) =>
        IsCriticalLease(lease)
        && (
            (lease.Status == ExecutionLeaseStatus.Leased && lease.CriticalApprovalGranted && !lease.CriticalApprovalConsumed)
            || lease.Status == ExecutionLeaseStatus.Running
            || lease.Status == ExecutionLeaseStatus.Verifying);

    public static string? ValidateNewLease(
        WorkTask task,
        ExecutionLease? existingActive,
        ExecutionLease? globalCriticalOccupant,
        bool humanApprovedCritical)
    {
        if (existingActive is not null)
            return "Card already has an active execution lease.";

        if (task.Status == WorkTaskStatus.Complete)
            return "Cannot lease a completed card.";

        var leaseRisk = LeaseRiskMapper.FromTaskRisk(task.Risk);
        if (leaseRisk == LeaseRiskLevel.Critical)
        {
            if (!humanApprovedCritical)
                return "Critical card requires human approval before execution.";
            if (globalCriticalOccupant is not null)
                return "Another critical lease is already active globally.";
        }

        return null;
    }

    public static string? ValidateCriticalDispatch(ExecutionLease lease)
    {
        if (!IsCriticalLease(lease))
            return null;

        if (!lease.CriticalApprovalGranted)
            return "Critical dispatch requires prior human approval on this lease.";

        if (lease.CriticalApprovalConsumed)
            return "Critical approval was already consumed by a dispatch attempt.";

        return null;
    }

    public static string? ValidateTransition(
        StatusChangeActor actor,
        ExecutionLeaseStatus from,
        ExecutionLeaseStatus to,
        string? reason = null)
    {
        if (IsTerminal(from))
            return $"Lease is terminal ({from}) and cannot change.";

        if (IsTerminal(to))
            return $"Use the dedicated human revoke/merge path for {to}, not a generic transition.";

        return actor switch
        {
            StatusChangeActor.DietCode => ValidateAgentTransition(from, to, reason),
            StatusChangeActor.System => ValidateSystemTransition(from, to),
            StatusChangeActor.Human => ValidateHumanTransition(from, to),
            _ => "Unknown actor.",
        };
    }

    public static string? ValidateAgentTransition(
        ExecutionLeaseStatus from,
        ExecutionLeaseStatus to,
        string? reason)
    {
        if (!AgentAllowedLeaseTargets.Contains(to))
            return $"DietCode cannot move lease to {to}.";

        if (to == ExecutionLeaseStatus.ReadyForReview)
            return "DietCode cannot move directly to ready_for_review; submit verification instead.";

        return to switch
        {
            ExecutionLeaseStatus.Blocked when from is ExecutionLeaseStatus.Running
                or ExecutionLeaseStatus.Blocked
                or ExecutionLeaseStatus.Verifying =>
                string.IsNullOrWhiteSpace(reason)
                    ? "Blocked transition requires a reason."
                    : null,
            ExecutionLeaseStatus.Blocked =>
                "Can only enter blocked from running, blocked, or verifying.",
            ExecutionLeaseStatus.Verifying when from is ExecutionLeaseStatus.Running
                or ExecutionLeaseStatus.Blocked =>
                null,
            ExecutionLeaseStatus.Verifying =>
                "Can only enter verifying from running or blocked.",
            _ => $"Agent cannot set lease status to {to}.",
        };
    }

    public static string? ValidateSystemTransition(ExecutionLeaseStatus from, ExecutionLeaseStatus to) =>
        (from, to) switch
        {
            (ExecutionLeaseStatus.Leased, ExecutionLeaseStatus.Running) => null,
            (ExecutionLeaseStatus.Leased, ExecutionLeaseStatus.Blocked) => null,
            (ExecutionLeaseStatus.Leased, ExecutionLeaseStatus.Revoked) => null,
            (ExecutionLeaseStatus.Running, ExecutionLeaseStatus.Blocked) => null,
            (ExecutionLeaseStatus.Verifying, ExecutionLeaseStatus.Blocked) => null,
            (ExecutionLeaseStatus.Blocked, ExecutionLeaseStatus.Revoked) => null,
            (ExecutionLeaseStatus.Blocked, ExecutionLeaseStatus.Leased) => null,
            (ExecutionLeaseStatus.ReadyForReview, ExecutionLeaseStatus.Blocked) => null,
            _ => $"System cannot transition from {from} to {to}.",
        };

    public static string? ValidateHumanTransition(ExecutionLeaseStatus from, ExecutionLeaseStatus to) =>
        (from, to) switch
        {
            (_, ExecutionLeaseStatus.Revoked) when ActiveLeaseStatuses.Contains(from) => null,
            (ExecutionLeaseStatus.ReadyForReview, ExecutionLeaseStatus.Merged) => null,
            _ => $"Human cannot transition from {from} to {to} via generic transition.",
        };

    public static string? ValidateAgentTaskStatusChange(WorkTaskStatus target) =>
        target == WorkTaskStatus.Complete
            ? "DietCode cannot mark cards done; human approval is required."
            : null;

    public static string? ValidateHumanMerge(ExecutionLease lease)
    {
        if (lease.Status != ExecutionLeaseStatus.ReadyForReview)
            return "Merge requires lease in ready_for_review.";

        if (string.IsNullOrWhiteSpace(lease.VerificationReportJson))
            return "Merge requires a successful verification report on the lease.";

        try
        {
            var report = VerificationReportSerializer.Deserialize(lease.VerificationReportJson);
            if (!VerificationReportValidator.IsPassingReport(report))
                return "Merge requires verification where all commands passed.";
        }
        catch
        {
            return "Stored verification report is invalid.";
        }

        return null;
    }

    public static string? ValidateHumanRevoke(ExecutionLease lease)
    {
        if (IsTerminal(lease.Status))
            return null;
        return IsActiveLease(lease) ? null : "Cannot revoke a non-active lease.";
    }

    public static WorkTaskStatus MapLeaseStatusToTaskStatus(ExecutionLeaseStatus leaseStatus) =>
        leaseStatus switch
        {
            ExecutionLeaseStatus.Leased or ExecutionLeaseStatus.Running => WorkTaskStatus.InProgress,
            ExecutionLeaseStatus.Blocked => WorkTaskStatus.Blocked,
            ExecutionLeaseStatus.Verifying => WorkTaskStatus.Verifying,
            ExecutionLeaseStatus.ReadyForReview => WorkTaskStatus.NeedsApproval,
            ExecutionLeaseStatus.Revoked => WorkTaskStatus.Blocked,
            ExecutionLeaseStatus.Merged => WorkTaskStatus.Complete,
            _ => WorkTaskStatus.InProgress,
        };
}
