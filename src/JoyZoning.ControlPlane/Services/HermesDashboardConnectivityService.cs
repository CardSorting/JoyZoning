using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Enums;

namespace JoyZoning.ControlPlane.Services;

public class HermesDashboardConnectivityService
{
    private readonly HermesDashboardService _dashboard;
    private readonly HermesProcessService _gateway;
    private readonly HermesHttpClient _hermesClient;
    private readonly ConfigService _config;
    private readonly KanbanSyncService _kanbanSync;

    public HermesDashboardConnectivityService(
        HermesDashboardService dashboard,
        HermesProcessService gateway,
        HermesHttpClient hermesClient,
        ConfigService config,
        KanbanSyncService kanbanSync)
    {
        _dashboard = dashboard;
        _gateway = gateway;
        _hermesClient = hermesClient;
        _config = config;
        _kanbanSync = kanbanSync;
    }

    public async Task<DashboardStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _config.GetSettingsAsync(cancellationToken);
        var reachable = await _dashboard.IsReachableAsync(cancellationToken);
        var hasToken = !string.IsNullOrWhiteSpace(settings.DashboardSessionToken);
        var tokenValid = hasToken && reachable &&
            await _dashboard.ValidateTokenAsync(settings.DashboardSessionToken, cancellationToken);

        return new DashboardStatusDto(
            reachable,
            hasToken,
            tokenValid,
            _dashboard.HermesCliFound,
            settings.DashboardBaseUrl,
            BuildMessage(reachable, hasToken, tokenValid));
    }

    public async Task<DashboardEnsureResult> EnsureAsync(
        bool alsoEnsureGateway = true,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<DashboardStepDto>();
        void Add(string id, string label, string status, string? detail = null) =>
            steps.Add(new DashboardStepDto(id, label, status, detail));

        if (alsoEnsureGateway)
        {
            Add("gateway", "Hermes API gateway", "running");
            await _gateway.EnsureGatewayRunningAsync(cancellationToken);
            var health = await _hermesClient.GetHealthAsync(cancellationToken);
            if (health.State != HealthState.Healthy)
            {
                Add("gateway", "Hermes API gateway", "failed", health.Message);
                return new DashboardEnsureResult(
                    false, false, false, false,
                    health.Message,
                    steps);
            }

            Add("gateway", "Hermes API gateway", "done", "Healthy");
        }

        if (!_dashboard.HermesCliFound)
        {
            Add("cli", "Hermes CLI", "failed", "hermes not found on PATH or in install root");
            return new DashboardEnsureResult(
                false, false, false, false,
                "Could not find hermes executable. Set install root in Settings.",
                steps);
        }

        Add("cli", "Hermes CLI", "done");

        var reachable = await _dashboard.IsReachableAsync(cancellationToken);
        if (!reachable)
        {
            Add("spawn", "Start dashboard", "running");
            await _dashboard.StartDashboardAsync(cancellationToken);
            Add("wait", "Wait for dashboard", "running");
            reachable = await _dashboard.WaitForReachableAsync(TimeSpan.FromSeconds(90), cancellationToken);
            if (!reachable)
            {
                Add("spawn", "Start dashboard", "failed");
                Add("wait", "Wait for dashboard", "failed", "Timed out waiting for /api/status");
                return new DashboardEnsureResult(
                    false, false, false, false,
                    "Dashboard did not become reachable. Try running `hermes dashboard` manually.",
                    steps);
            }

            Add("spawn", "Start dashboard", "done");
            Add("wait", "Wait for dashboard", "done");
        }
        else
        {
            Add("spawn", "Start dashboard", "skipped", "Already running");
            Add("wait", "Wait for dashboard", "done");
        }

        Add("token", "Acquire session token", "running");
        var token = await _dashboard.ScrapeSessionTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            Add("token", "Acquire session token", "failed", "Could not read token from dashboard HTML");
            return new DashboardEnsureResult(
                true, false, false, false,
                "Dashboard is up but session token could not be acquired.",
                steps);
        }

        await _config.UpdateDashboardTokenAsync(token, cancellationToken);
        Add("token", "Acquire session token", "done");

        var valid = await _dashboard.ValidateTokenAsync(token, cancellationToken);
        return new DashboardEnsureResult(
            true,
            true,
            valid,
            valid,
            valid
                ? "Dashboard connected. Kanban sync and Hermes TUI are ready."
                : "Token saved but kanban API validation failed — try Reconnect after dashboard restarts.",
            steps);
    }

    public async Task<DashboardEnsureResult> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var steps = new List<DashboardStepDto>();
        if (!await _dashboard.IsReachableAsync(cancellationToken))
        {
            return new DashboardEnsureResult(
                false, false, false, false,
                "Dashboard is not reachable.",
                [new DashboardStepDto("token", "Refresh session token", "failed", "Dashboard offline")]);
        }

        var token = await _dashboard.ScrapeSessionTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            return new DashboardEnsureResult(
                true, false, false, false,
                "Could not scrape a new session token.",
                [new DashboardStepDto("token", "Refresh session token", "failed", null)]);
        }

        await _config.UpdateDashboardTokenAsync(token, cancellationToken);
        var valid = await _dashboard.ValidateTokenAsync(token, cancellationToken);
        return new DashboardEnsureResult(
            true, true, valid, valid,
            valid ? "Session token refreshed." : "Token refreshed but validation failed.",
            [new DashboardStepDto("token", "Refresh session token", "done", null)]);
    }

    private static string BuildMessage(bool reachable, bool hasToken, bool tokenValid)
    {
        if (!reachable) return "Dashboard not running";
        if (!hasToken) return "Dashboard reachable — connect to acquire token";
        return tokenValid ? "Dashboard connected" : "Token may be stale — reconnect";
    }
}

public record DashboardStatusDto(
    bool Reachable,
    bool HasToken,
    bool TokenValid,
    bool HermesCliFound,
    string DashboardUrl,
    string Message);

public record DashboardStepDto(string Id, string Label, string Status, string? Detail);

public record DashboardEnsureResult(
    bool Reachable,
    bool TokenAcquired,
    bool TokenValid,
    bool Ready,
    string Message,
    IReadOnlyList<DashboardStepDto> Steps);
