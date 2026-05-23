namespace JoyZoning.Agents.Hermes;

/// <summary>Refreshes the Hermes dashboard session token when kanban API returns 401/403.</summary>
public interface IDashboardTokenRefresher
{
    Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default);
}
