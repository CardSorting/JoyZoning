namespace JoyZoning.Domain.Orchestration;

/// <summary>
/// Authority routing for the Hermes-runtime reversal.
/// JoyZoning (habitat) observes, reviews, and accept-merges — it must not execute tools
/// or treat mirrored runtime events as authoritative operational state.
/// </summary>
public static class HabitatLayerGuardrails
{
    public const string HermesRuntimeSource = "hermes-runtime";

    /// <summary>Habitat may never claim execution authority via observation ingest.</summary>
    public static bool RejectsAuthoritativeObservation(bool authoritative) => authoritative;

    /// <summary>
    /// Event types the habitat may emit locally (operator / merge / lease only).
    /// Anything implying tool dispatch or workspace mutation outside lease/merge is forbidden.
    /// </summary>
    public static bool HabitatMayEmitEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return false;

        if (eventType.StartsWith("operator.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("lease.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("merge.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("verification.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("habitat.", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>Forbidden habitat-side patterns that imply direct execution.</summary>
    public static bool HabitatMustNotEmitEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return true;

        return eventType.StartsWith("tool.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("terminal.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("mutation.patch", StringComparison.OrdinalIgnoreCase)
            || eventType.Equals("convergence.converged", StringComparison.OrdinalIgnoreCase)
            || eventType.Equals("convergence.ready_for_review", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHermesOwnedConvergenceEvent(string eventType) =>
        eventType.StartsWith("convergence.", StringComparison.OrdinalIgnoreCase)
        || eventType.StartsWith("mutation.", StringComparison.OrdinalIgnoreCase);
}
