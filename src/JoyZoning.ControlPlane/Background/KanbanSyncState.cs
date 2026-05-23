using JoyZoning.Agents.Hermes;

namespace JoyZoning.ControlPlane.Background;

public class KanbanSyncState
{
    public DateTimeOffset? LastSyncAt { get; set; }
    public string LastMessage { get; set; } = "Not synced yet";
    public KanbanSyncOutcome? LastOutcome { get; set; }
    public DateTimeOffset? LastAuthFailureAt { get; set; }
}
