namespace JoyZoning.Domain.Orchestration;

/// <summary>Non-authoritative read model derived from ingested Hermes observations.</summary>
public class HermesObservationReadModel
{
    private readonly object _lock = new();
    private readonly Dictionary<string, HermesConvergenceObservation> _byScope = new(StringComparer.OrdinalIgnoreCase);

    public void ApplyObservation(HermesObservationSnapshot snapshot)
    {
        var scopeKey = snapshot.ScopeId ?? snapshot.SessionId ?? "";
        if (string.IsNullOrWhiteSpace(scopeKey))
            return;

        if (!HabitatLayerGuardrails.IsHermesOwnedConvergenceEvent(snapshot.Type))
            return;

        var state = InferConvergenceState(snapshot.Type, snapshot.StateHint);
        var entry = new HermesConvergenceObservation(
            scopeKey,
            state,
            snapshot.Type,
            snapshot.Layer,
            snapshot.RunId,
            snapshot.ObservedAt);

        lock (_lock)
        {
            _byScope[scopeKey] = entry;
        }
    }

    public HermesConvergenceObservation? GetConvergence(string scopeId)
    {
        lock (_lock)
        {
            return _byScope.TryGetValue(scopeId, out var v) ? v : null;
        }
    }

    private static string InferConvergenceState(string eventType, string? stateHint)
    {
        if (!string.IsNullOrWhiteSpace(stateHint))
            return stateHint;

        return eventType switch
        {
            "convergence.ready_for_review" => "ready_for_review",
            "convergence.converged" => "converged",
            "convergence.rejected" => "rejected",
            "convergence.review_requested" => "review_requested",
            "mutation.proposed" => "mutation_proposed",
            "mutation.verified" => "mutation_verified",
            _ => eventType,
        };
    }
}

public record HermesObservationSnapshot(
    string Type,
    string Layer,
    string? ScopeId,
    string? SessionId,
    string? RunId,
    string? StateHint,
    DateTimeOffset ObservedAt);

public record HermesConvergenceObservation(
    string ScopeId,
    string State,
    string LastEventType,
    string Layer,
    string? RunId,
    DateTimeOffset ObservedAt);

public static class HermesJournalAdapter
{
    public const string CanonicalJournalHint =
        "~/.hermes/joyzoning/journal.db (Hermes-owned; habitat ingest is a non-authoritative mirror)";

    public static string DisplayNote =>
        "Convergence state mirrored from Hermes (observe-only). Accept-merge remains operator-owned in JoyZoning.";
}
