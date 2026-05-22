namespace JoyZoning.Domain.Enums;

public enum SessionStatus
{
    Idle = 0,
    ManagerActive = 1,
    Executing = 2,
    AwaitingApproval = 3,
    Paused = 4,
    Error = 5,
}
