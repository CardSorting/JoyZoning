namespace JoyZoning.Domain.Configuration;

public class BroccoliQOptions
{
    public const string SectionName = "BroccoliQ";

    /// <summary>Mirror joy_events into BroccoliQ hive_audit and enable the bridge worker.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Spawn broccoliq/worker/joy-bridge.mjs when the control plane starts.</summary>
    public bool AutoStartWorker { get; set; } = true;

    /// <summary>Periodically verify bridge health and restart the worker if it died.</summary>
    public bool SupervisorEnabled { get; set; } = true;

    public string BridgeListenUrl { get; set; } = "http://127.0.0.1:9471";

    /// <summary>SQLite path for the hive; defaults to broccoliq.db beside joyzoning.db.</summary>
    public string? DatabasePath { get; set; }

    /// <summary>Repo root containing broccoliq/; auto-discovered when unset.</summary>
    public string? RepoRoot { get; set; }

    /// <summary>Per-request HTTP timeout for bridge calls.</summary>
    public int EventMirrorTimeoutMs { get; set; } = 800;

    public int WorkerStartupTimeoutSeconds { get; set; } = 20;

    /// <summary>How often the supervisor re-checks bridge health (seconds).</summary>
    public int WorkerSupervisorIntervalSeconds { get; set; } = 30;

    /// <summary>Max joy_events waiting in the in-process mirror queue.</summary>
    public int MirrorQueueCapacity { get; set; } = 8192;

    /// <summary>Max events per batch POST to the bridge.</summary>
    public int MirrorBatchSize { get; set; } = 64;

    /// <summary>Max wait before flushing a partial batch (milliseconds).</summary>
    public int MirrorFlushIntervalMs { get; set; } = 75;

    /// <summary>Max JSON body size accepted by joy-bridge (bytes).</summary>
    public int MaxRequestBodyBytes { get; set; } = 1_048_576;

    /// <summary>After the bridge is healthy, replay joy_events not yet mirrored (cursor in config).</summary>
    public bool BackfillOnStartup { get; set; } = true;

    /// <summary>Max events per backfill run (startup or manual).</summary>
    public int BackfillMaxEvents { get; set; } = 5000;

    /// <summary>Upsert all work_tasks into hive_tasks during backfill.</summary>
    public bool BackfillTasksOnStartup { get; set; } = true;

    /// <summary>HTTP batch retries when the bridge is temporarily unavailable.</summary>
    public int MirrorPumpRetryAttempts { get; set; } = 3;
}

