namespace JoyZoning.Domain.Orchestration;

/// <summary>Observed health of a per-worker live mirror (read model).</summary>
public enum LiveMirrorHealthState
{
    Active = 0,
    Stale = 1,
    Completed = 2,
    SkippedCollision = 3,
    SkippedParallelSharedRootGuard = 4,
    FailedCopy = 5,
    Pruned = 6,
    Unknown = 7,
}

public static class LiveMirrorHealthStateNames
{
    public static string ToApiString(LiveMirrorHealthState state) =>
        state switch
        {
            LiveMirrorHealthState.Active => "active",
            LiveMirrorHealthState.Stale => "stale",
            LiveMirrorHealthState.Completed => "completed",
            LiveMirrorHealthState.SkippedCollision => "skipped_collision",
            LiveMirrorHealthState.SkippedParallelSharedRootGuard => "skipped_parallel_shared_root_guard",
            LiveMirrorHealthState.FailedCopy => "failed_copy",
            LiveMirrorHealthState.Pruned => "pruned",
            _ => "unknown",
        };

    public static LiveMirrorHealthState Parse(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "active" => LiveMirrorHealthState.Active,
            "stale" => LiveMirrorHealthState.Stale,
            "completed" => LiveMirrorHealthState.Completed,
            "skipped_collision" => LiveMirrorHealthState.SkippedCollision,
            "skipped_parallel_shared_root_guard" => LiveMirrorHealthState.SkippedParallelSharedRootGuard,
            "failed_copy" => LiveMirrorHealthState.FailedCopy,
            "pruned" => LiveMirrorHealthState.Pruned,
            _ => LiveMirrorHealthState.Unknown,
        };
}
