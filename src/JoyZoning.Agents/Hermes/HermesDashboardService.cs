using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Agents.Hermes;

/// <summary>
/// Starts the Hermes web dashboard and acquires its ephemeral session token (scraped from index.html).
/// </summary>
public sealed class HermesDashboardService
{
    private static readonly Regex TokenRegex = new(
        @"window\.__HERMES_SESSION_TOKEN__\s*=\s*""([^""]+)""",
        RegexOptions.Compiled);

    private readonly HermesRuntimeSettings _settings;
    private readonly ILogger<HermesDashboardService> _logger;
    private readonly HttpClient _http;
    private Process? _dashboardProcess;

    public HermesDashboardService(HermesRuntimeSettings settings, ILogger<HermesDashboardService> logger)
    {
        _settings = settings;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
    }

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await _http.GetAsync($"{_settings.DashboardBaseUrl}/api/status", cancellationToken);
            return r.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> ScrapeSessionTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _http.GetStringAsync($"{_settings.DashboardBaseUrl}/", cancellationToken);
            var m = TokenRegex.Match(html);
            return m.Success ? m.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to scrape dashboard session token");
            return null;
        }
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_settings.DashboardBaseUrl}/api/plugins/kanban/board");
            request.Headers.TryAddWithoutValidation("X-Hermes-Session-Token", token);
            var r = await _http.SendAsync(request, cancellationToken);
            return r.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> WaitForReachableAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
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
        if (_dashboardProcess is { HasExited: false })
            return Task.CompletedTask;

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var hermesCmd = HermesCliLocator.FindHermesExecutable(_settings.InstallRoot);
        if (hermesCmd is null)
        {
            _logger.LogWarning("Could not find hermes executable for dashboard");
            return Task.CompletedTask;
        }

        var args = string.IsNullOrEmpty(_settings.Profile)
            ? "dashboard --no-open --tui"
            : $"-p {_settings.Profile} dashboard --no-open --tui";

        var psi = new ProcessStartInfo
        {
            FileName = hermesCmd,
            Arguments = args,
            WorkingDirectory = _settings.InstallRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _dashboardProcess = Process.Start(psi);
        _logger.LogInformation("Started Hermes dashboard: {File} {Args}", hermesCmd, args);
        return Task.CompletedTask;
    }

    public bool HermesCliFound => HermesCliLocator.FindHermesExecutable(_settings.InstallRoot) is not null;
}
