namespace JoyZoning.Domain.Configuration;

/// <summary>Kanban and execution tuning for sequential JSDP delivery.</summary>
public class WorkspaceParallelismOptions
{
    public const string SectionName = "WorkspaceParallelism";

    /// <summary>Skip full two-way kanban sync while multiple active leases exist (should stay 2+; JSDP uses one).</summary>
    public int DeferFullKanbanSyncWhenActiveLeasesAtLeast { get; set; } = 2;

    public int KanbanOutboxPollIntervalMs { get; set; } = 250;

    /// <summary>Changed-file count at or above this value sets the <c>large_change_set</c> risk flag.</summary>
    public int LargeChangeSetFileThreshold { get; set; } = 20;
}
