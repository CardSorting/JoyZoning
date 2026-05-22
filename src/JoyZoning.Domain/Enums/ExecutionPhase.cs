namespace JoyZoning.Domain.Enums;

public enum ExecutionPhase
{
    Starting = 0,
    Running = 1,
    AwaitingApproval = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Interrupted = 6,
}
