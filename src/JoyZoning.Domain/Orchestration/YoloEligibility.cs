using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public sealed record YoloTaskCandidate(
    Guid TaskId,
    string Title,
    WorkTaskStatus Status,
    RiskLevel Risk,
    IReadOnlyList<string> Tags,
    string? SkipReason = null)
{
    public bool IsEligible => SkipReason is null;
}

public static class YoloEligibility
{
    public static IReadOnlyList<WorkTaskStatus> GetPickupStatuses(YoloPolicy policy)
    {
        var list = new List<WorkTaskStatus>
        {
            WorkTaskStatus.Backlog,
            WorkTaskStatus.Planned,
        };
        if (policy.AllowRecover)
            list.Add(WorkTaskStatus.Blocked);
        return list;
    }

    public static YoloTaskCandidate Evaluate(
        Guid taskId,
        string title,
        string? description,
        int status,
        int risk,
        bool hasActiveLease,
        ExecutionLeaseStatus? activeLeaseStatus,
        YoloPolicy policy)
    {
        var taskStatus = (WorkTaskStatus)status;
        var taskRisk = (RiskLevel)risk;
        var tags = YoloTaskTags.Extract(title, description);

        string? skip = null;

        if (!policy.Enabled)
            skip = "YOLO policy is disabled.";

        if (!GetPickupStatuses(policy).Contains(taskStatus))
            skip ??= $"Task status {taskStatus} is not eligible for autonomous pickup.";

        if (hasActiveLease)
        {
            if (activeLeaseStatus == ExecutionLeaseStatus.Blocked && policy.AllowRecover)
            {
                // Runner will reopen blocked lease then dispatch-retry.
            }
            else
                skip ??= "Task already has an active lease.";
        }

        if (taskRisk > policy.MaxRiskLevelEnum())
            skip ??= $"Task risk {taskRisk} exceeds policy maxRiskLevel ({policy.MaxRiskLevel}).";

        if (taskRisk is RiskLevel.High or RiskLevel.Critical)
            skip ??= "High/critical tasks require prior human approval; YOLO will not dispatch them.";

        if (!YoloTaskTags.MatchesPolicy(tags, policy.AllowedTaskTags, policy.ForbiddenTaskTags, out var tagReason))
            skip ??= tagReason;

        return new YoloTaskCandidate(taskId, title, taskStatus, taskRisk, tags, skip);
    }

    public static bool IsCriticalRisk(RiskLevel risk) =>
        risk is RiskLevel.High or RiskLevel.Critical;

    public static bool LeaseHasPreApprovedCritical(
        LeaseRiskLevel leaseRisk,
        bool criticalApprovalGranted,
        bool criticalApprovalConsumed) =>
        leaseRisk == LeaseRiskLevel.Critical
        && criticalApprovalGranted
        && !criticalApprovalConsumed;
}
