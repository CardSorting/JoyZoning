using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Enums;

namespace JoyZoning.ControlPlane.Services;

public class HermesConnectorDiagnostics
{
    private readonly HermesHttpClient _api;
    private readonly HermesRuntimeSettings _runtime;
    private readonly KanbanSyncService _kanban;
    private readonly HermesProcessService _gateway;

    public HermesConnectorDiagnostics(
        HermesHttpClient api,
        HermesRuntimeSettings runtime,
        KanbanSyncService kanban,
        HermesProcessService gateway)
    {
        _api = api;
        _runtime = runtime;
        _kanban = kanban;
        _gateway = gateway;
    }

    public async Task<HermesConnectorReport> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _runtime.GetSnapshot();
        var checks = new List<HermesConnectorCheck>();

        _runtime.ReloadApiKeyFromProfile();
        _api.RefreshConnection();
        snapshot = _runtime.GetSnapshot();

        var hasKey = !string.IsNullOrWhiteSpace(snapshot.ApiKey);
        checks.Add(new HermesConnectorCheck(
            "api_key",
            hasKey ? HealthState.Healthy : HealthState.Unavailable,
            hasKey
                ? $"API key loaded for profile '{snapshot.Profile}'"
                : $"No API key for profile '{snapshot.Profile}'"));

        if (hasKey)
        {
            var authOk = await _api.VerifyApiKeyAsync(cancellationToken);
            checks.Add(new HermesConnectorCheck(
                "api_auth",
                authOk ? HealthState.Healthy : HealthState.Unavailable,
                authOk
                    ? "Gateway accepts API key"
                    : "Gateway rejected API key — run POST /api/hermes/sync-credentials or restart gateway"));
        }

        var apiHealth = await _api.GetHealthAsync(cancellationToken);
        checks.Add(new HermesConnectorCheck("api_health", apiHealth.State, apiHealth.Message));

        var dashboardReachable = await _kanban.IsDashboardReachableAsync(cancellationToken);
        checks.Add(new HermesConnectorCheck(
            "dashboard_reachable",
            dashboardReachable ? HealthState.Healthy : HealthState.Degraded,
            dashboardReachable ? "Dashboard /api/status OK" : "Dashboard not reachable"));

        var board = await _kanban.FetchBoardTasksAsync(cancellationToken);
        checks.Add(new HermesConnectorCheck(
            "kanban_board",
            board.Outcome switch
            {
                KanbanSyncOutcome.Success => HealthState.Healthy,
                KanbanSyncOutcome.SkippedNoToken => HealthState.Degraded,
                KanbanSyncOutcome.Unauthorized => HealthState.Unavailable,
                _ => HealthState.Degraded,
            },
            board.Detail ?? $"{board.Outcome} ({board.Tasks.Count} tasks)"));

        var gatewayReady = await _gateway.EnsureGatewayRunningAsync(cancellationToken);
        checks.Add(new HermesConnectorCheck(
            "gateway",
            gatewayReady ? HealthState.Healthy : HealthState.Degraded,
            gatewayReady ? "Gateway reachable" : "Gateway not ready"));

        var overall = checks.Any(c => c.State == HealthState.Unavailable)
            ? HealthState.Unavailable
            : checks.Any(c => c.State == HealthState.Degraded)
                ? HealthState.Degraded
                : HealthState.Healthy;

        return new HermesConnectorReport(overall, checks);
    }
}

public record HermesConnectorCheck(string Id, HealthState State, string Detail);

public record HermesConnectorReport(HealthState Overall, IReadOnlyList<HermesConnectorCheck> Checks);
