namespace JoyZoning.Domain.Orchestration;

/// <summary>Observed merge/reconciliation state for a worker lease (read model).</summary>
public enum WorkerMergeState
{
    Running = 0,
    ReadyToMerge = 1,
    Merging = 2,
    Merged = 3,
    MergeConflict = 4,
    MergeFailed = 5,
    Revoked = 6,
    Abandoned = 7,
    Stale = 8,
}

public static class WorkerMergeStateNames
{
    public static string ToApiString(WorkerMergeState state) =>
        state switch
        {
            WorkerMergeState.Running => "running",
            WorkerMergeState.ReadyToMerge => "ready_to_merge",
            WorkerMergeState.Merging => "merging",
            WorkerMergeState.Merged => "merged",
            WorkerMergeState.MergeConflict => "merge_conflict",
            WorkerMergeState.MergeFailed => "merge_failed",
            WorkerMergeState.Revoked => "revoked",
            WorkerMergeState.Abandoned => "abandoned",
            WorkerMergeState.Stale => "stale",
            _ => "running",
        };

    public static WorkerMergeState Parse(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "ready_to_merge" => WorkerMergeState.ReadyToMerge,
            "merging" => WorkerMergeState.Merging,
            "merged" => WorkerMergeState.Merged,
            "merge_conflict" => WorkerMergeState.MergeConflict,
            "merge_failed" => WorkerMergeState.MergeFailed,
            "revoked" => WorkerMergeState.Revoked,
            "abandoned" => WorkerMergeState.Abandoned,
            "stale" => WorkerMergeState.Stale,
            _ => WorkerMergeState.Running,
        };
}
