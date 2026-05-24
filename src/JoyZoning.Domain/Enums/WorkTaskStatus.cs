namespace JoyZoning.Domain.Enums;

public enum WorkTaskStatus
{
    Backlog = 0,
    Planned = 1,
    InProgress = 2,
    NeedsApproval = 3,
    Verifying = 4,
    Blocked = 5,
    Complete = 6,
    ReadyToStart = 7,
    HermesRunning = 8,
    ExternalInProgress = 9,
    ReadyForReview = 10,
    Verified = 11,
    Failed = 12,
}
