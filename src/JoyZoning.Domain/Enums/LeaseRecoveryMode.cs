namespace JoyZoning.Domain.Enums;

/// <summary>How a human/system recovery flow resumes work on a card.</summary>
public enum LeaseRecoveryMode
{
    /// <summary>Blocked lease returns to leased; same worktree and lease id.</summary>
    ReopenBlocked = 0,

    /// <summary>Rebind active/blocked lease to existing worktree; refresh timers.</summary>
    ReattachWorktree = 1,

    /// <summary>Terminal prior lease with evidence; new lease links via RecoveredFromLeaseId.</summary>
    ReplacementLease = 2,
}
