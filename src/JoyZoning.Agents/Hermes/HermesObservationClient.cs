using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Agents.Hermes;

/// <summary>
/// Habitat-side adapter for Hermes runtime observations (observe-only).
/// Does not dispatch tools or mutate workspace files.
/// </summary>
public class HermesObservationClient
{
    private readonly HermesObservationReadModel _readModel;

    public HermesObservationClient(HermesObservationReadModel readModel)
    {
        _readModel = readModel;
    }

    /// <summary>
    /// Latest Hermes-owned convergence state for a scope (task/session id).
    /// Returns null when no observations have been ingested yet.
    /// </summary>
    public HermesConvergenceObservation? GetConvergenceState(string scopeId) =>
        string.IsNullOrWhiteSpace(scopeId) ? null : _readModel.GetConvergence(scopeId);
}
