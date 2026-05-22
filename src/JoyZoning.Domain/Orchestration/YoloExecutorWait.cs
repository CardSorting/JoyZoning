using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Lease statuses YOLO accepts before running local verification.</summary>
public static class YoloExecutorWait
{
    public static readonly ExecutionLeaseStatus[] ReadyForVerificationStatuses =
    [
        ExecutionLeaseStatus.Leased,
        ExecutionLeaseStatus.Running,
        ExecutionLeaseStatus.Blocked,
        ExecutionLeaseStatus.Verifying,
        ExecutionLeaseStatus.ReadyForReview,
    ];

    public static bool IsReadyForVerification(ExecutionLeaseStatus leaseStatus) =>
        ReadyForVerificationStatuses.Contains(leaseStatus);

    public static bool IsTerminalFailure(ExecutionLeaseStatus leaseStatus) =>
        leaseStatus is ExecutionLeaseStatus.Revoked or ExecutionLeaseStatus.Merged;
}
