using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Agents.Hermes;

/// <summary>
/// Starts the Hermes web dashboard and acquires its ephemeral session token (scraped from index.html).
/// </summary>
public sealed class HermesDashboardService : IDisposable
{
    public const string HttpClientName = "hermes-dashboard";

    private static readonly Regex TokenRegex = new(
        @"window\.__HERMES_SESSION_TOKEN__\s*=\s*""([^""]+)""",
        RegexOptions.Compiled);

    private readonly HermesRuntimeSettings _settings;
    private readonly ILogger<HermesDashboardService> _logger;
    private readonly IHttpClientFactory _httpFactory;
    private readonly object _processLock = new();
    private Process? _dashboardProcess;

    public HermesDashboardService(
        HermesRuntimeSettings settings,
        IHttpClientFactory httpFactory,
        ILogger<HermesDashboardService> logger,
        IHostApplicationLifetime? lifetime = null)
    {
        _settings = settings;
        _httpFactory = httpFactory;
        _logger = logger;
        lifetime?.ApplicationStopping.Register(StopDashboardProcess);
    }

    public void Dispose() => StopDashboardProcess();

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            var dashboard = _settings.GetSnapshot().DashboardBaseUrl;
            var r = await client.GetAsync($"{dashboard}/api/status", cancellationToken);
            return r.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dashboard status check failed");
            return false;
        }
    }

    public async Task<string?> ScrapeSessionTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            var dashboard = _settings.GetSnapshot().DashboardBaseUrl;
            var html = await client.GetStringAsync($"{dashboard}/", cancellationToken);
            var m = TokenRegex.Match(html);
            return m.Success ? m.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scrape dashboard session token");
            return null;
        }
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_settings.GetSnapshot().DashboardBaseUrl}/api/plugins/kanban/board");
            request.Headers.TryAddWithoutValidation("X-Hermes-Session-Token", token);
            var r = await client.SendAsync(request, cancellationToken);
            return r.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dashboard token validation failed");
            return false;
        }
    }

    public async Task<bool> WaitForReachableAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsReachableAsync(cancellationToken))
                return true;
            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    public Task StartDashboardAsync(CancellationToken cancellationToken = default)
    {
        lock (_processLock)
        {
            if (_dashboardProcess is { HasExited: false })
                return Task.CompletedTask;

            if (_dashboardProcess is { HasExited: true })
            {
                try { _dashboardProcess.Dispose(); } catch { /* ignore */ }
                _dashboardProcess = null;
            }

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            var snapshot = _settings.GetSnapshot();
            var hermesCmd = HermesCliLocator.FindHermesExecutable(snapshot.InstallRoot);
            if (hermesCmd is null)
            {
                _logger.LogWarning("Could not find hermes executable for dashboard");
                return Task.CompletedTask;
            }

            var args = string.IsNullOrEmpty(snapshot.Profile)
                ? "dashboard --no-open --tui"
                : $"-p {snapshot.Profile} dashboard --no-open --tui";

            var psi = new ProcessStartInfo
            {
                FileName = hermesCmd,
                Arguments = args,
                WorkingDirectory = snapshot.InstallRoot,
            };

            _dashboardProcess = HermesChildProcessHost.Start(psi, _logger, "hermes-dashboard");
            if (_dashboardProcess is null)
                _logger.LogWarning("Failed to start Hermes dashboard process");

            _logger.LogInformation("Started Hermes dashboard: {File} {Args}", hermesCmd, args);
        }

        return Task.CompletedTask;
    }

    public bool HermesCliFound =>
        HermesCliLocator.FindHermesExecutable(_settings.GetSnapshot().InstallRoot) is not null;

    private void StopDashboardProcess()
    {
        lock (_processLock)
        {
            if (_dashboardProcess is not { HasExited: false })
                return;
            try
            {
                _dashboardProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to stop Hermes dashboard on shutdown");
            }
            finally
            {
                _dashboardProcess.Dispose();
                _dashboardProcess = null;
            }
        }
    }
}
