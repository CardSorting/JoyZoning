namespace JoyZoning.Domain.Enums;

/// <summary>Lifecycle of a kanban card execution lease (not the kanban column itself).</summary>
public enum ExecutionLeaseStatus
{
    Leased = 0,
    Running = 1,
    Blocked = 2,
    Verifying = 3,
    ReadyForReview = 4,
    Revoked = 5,
    Merged = 6,
}
