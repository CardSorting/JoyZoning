namespace JoyZoning.Domain.Configuration;

/// <summary>Scheduler limits, lease duration, and stale thresholds for execution leases.</summary>
public class LeaseRuntimeOptions
{
    public const string SectionName = "LeaseRuntime";

    public int MaxGlobalActiveLeases { get; set; } = 4;
    /// <summary>Hard cap per session — default 1 (single bounded agent).</summary>
    public int MaxActiveLeasesPerSession { get; set; } = 1;
    public int MaxCriticalLeases { get; set; } = 1;
    public int ReconciliationIntervalSeconds { get; set; } = 30;

    /// <summary>Background scan of active lease worktrees for git/mtime changes.</summary>
    public bool WorktreeMonitorEnabled { get; set; } = true;

    public int WorktreeMonitorIntervalSeconds { get; set; } = 8;

    /// <summary>When true, absolute ExpiresAt transitions to revoked; otherwise blocked.</summary>
    public bool AbsoluteExpirationRevokes { get; set; }

    public LeaseDurationOptions Duration { get; set; } = new();
    public LeaseStaleOptions Stale { get; set; } = new();

    /// <summary>
    /// When true, <c>POST …/lease/merge</c> only updates lease/task metadata (legacy).
    /// Default false: git convergence must succeed before Merged/Complete.
    /// </summary>
    public bool MetadataOnlyAcceptResult { get; set; }

    /// <summary>When false, a dirty canonical workspace blocks accept (no auto-stash).</summary>
    public bool AllowDirtyDestination { get; set; }
}

public class LeaseDurationOptions
{
    public int LowHours { get; set; } = 8;
    public int MediumHours { get; set; } = 4;
    public int CriticalHours { get; set; } = 2;
}

public class LeaseStaleOptions
{
    public int LeasedMinutes { get; set; } = 90;
    public int RunningMinutes { get; set; } = 120;
    public int VerifyingMinutes { get; set; } = 120;
    public int CriticalLeasedMinutes { get; set; } = 10;
    public int CriticalRunningMinutes { get; set; } = 20;
    public int CriticalVerifyingMinutes { get; set; } = 45;
}
