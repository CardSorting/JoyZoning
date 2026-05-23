namespace JoyZoning.Domain.Configuration;

/// <summary>Where active lease worktrees are mirrored for IDE / watch visibility.</summary>
public enum LiveMirrorMode
{
    /// <summary>Copy into session workspace root (legacy; unsafe with parallel leases).</summary>
    SharedSessionRoot = 0,

    /// <summary>One mirror folder per task: <c>.joyzoning/live/&lt;task-id&gt;/</c>.</summary>
    PerTask = 1,

    /// <summary>
    /// One mirror folder per execution:
    /// <c>.joyzoning/live/&lt;task-id&gt;/&lt;execution-id&gt;/</c>.
    /// </summary>
    PerExecution = 2,
}
