namespace JoyZoning.Agents.Hermes;

public enum KanbanSyncOutcome
{
    Success = 0,
    SkippedNoToken = 1,
    Unauthorized = 2,
    Unavailable = 3,
    Error = 4,
}

public sealed record KanbanFetchResult(
    IReadOnlyList<HermesKanbanTaskSnapshot> Tasks,
    KanbanSyncOutcome Outcome,
    string? Detail = null)
{
    public static KanbanFetchResult Skipped(string detail) =>
        new(Array.Empty<HermesKanbanTaskSnapshot>(), KanbanSyncOutcome.SkippedNoToken, detail);

    public bool IsAuthFailure => Outcome is KanbanSyncOutcome.Unauthorized;
}

public sealed record KanbanWriteResult(
    bool Success,
    KanbanSyncOutcome Outcome,
    string? ResponseBody = null,
    string? Detail = null);
