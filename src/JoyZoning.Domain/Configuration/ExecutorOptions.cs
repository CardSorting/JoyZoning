namespace JoyZoning.Domain.Configuration;

/// <summary>DietCode executor behavior during leased dispatch runs.</summary>
public sealed class ExecutorOptions
{
    public const string SectionName = "Executor";

    /// <summary>
    /// When true, Hermes tool approval prompts during DietCode runs are approved automatically
    /// so executor work is not blocked waiting for a human click.
    /// </summary>
    public bool AutoApproveToolRequests { get; set; } = true;

    /// <summary>Hermes status polls when SSE closes before a terminal run event.</summary>
    public int StreamStatusPollAttempts { get; set; } = 8;

    /// <summary>Delay between stream-death status polls.</summary>
    public int StreamStatusPollIntervalSeconds { get; set; } = 2;

    /// <summary>Max times to re-attach SSE for the same run id (prevents infinite loops).</summary>
    public int MaxStreamResumeAttempts { get; set; } = 12;
}
