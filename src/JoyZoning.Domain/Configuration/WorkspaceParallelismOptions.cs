namespace JoyZoning.Domain.Configuration;

/// <summary>Kanban and Hermes parallelism tuning when multiple leases run on one workspace.</summary>
public class WorkspaceParallelismOptions
{
    public const string SectionName = "WorkspaceParallelism";

    /// <summary>
    /// Skip full two-way kanban sync for a workspace while at least this many active leases exist.
    /// Reduces pull storms while multiple agents mutate cards concurrently.
    /// </summary>
    public int DeferFullKanbanSyncWhenActiveLeasesAtLeast { get; set; } = 2;

    /// <summary>Delay between outbox drain attempts when the queue is idle.</summary>
    public int KanbanOutboxPollIntervalMs { get; set; } = 250;

    /// <summary>Isolated live mirror layout under <c>.joyzoning/live/</c>.</summary>
    public LiveMirrorMode LiveMirrorMode { get; set; } = LiveMirrorMode.PerExecution;

    /// <summary>
    /// When true and more than one active lease exists on a workspace, shared session-root mirroring is skipped
    /// even if <see cref="LiveMirrorMode"/> is <see cref="LiveMirrorMode.SharedSessionRoot"/>.
    /// </summary>
    public bool DisableSharedSessionRootMirrorWhenParallel { get; set; } = true;

    /// <summary>
    /// Delete completed live mirror folders older than this many days. 0 disables pruning.
    /// </summary>
    public int LiveMirrorRetentionDays { get; set; } = 14;

    /// <summary>Changed-file count at or above this value sets the <c>large_change_set</c> risk flag.</summary>
    public int LargeChangeSetFileThreshold { get; set; } = 20;
}
