using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JoyZoning.Agents.Hermes;

public class HermesHttpClient
{
    private readonly HttpClient _http;
    private readonly HermesRuntimeSettings _settings;
    private readonly ILogger<HermesHttpClient> _logger;

    public HermesHttpClient(HttpClient http, HermesRuntimeSettings settings, ILogger<HermesHttpClient> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
        SyncBaseAddress();
    }

    public void RefreshConnection() => SyncBaseAddress();

    private void SyncBaseAddress()
    {
        _http.BaseAddress = _settings.ApiUri;
        _http.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrEmpty(_settings.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<HealthStatus> GetHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode
                ? new HealthStatus(HealthState.Healthy, "Hermes API reachable")
                : new HealthStatus(HealthState.Degraded, $"Health returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hermes health check failed");
            return new HealthStatus(HealthState.Unavailable, ex.Message);
        }
    }

    public async Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            input = request.Prompt,
            session_id = request.SessionId,
            metadata = new
            {
                workspace_root = request.WorkspaceRoot,
                role = request.Role.ToString(),
            },
        };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("v1/runs", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var runId = doc.RootElement.TryGetProperty("run_id", out var rid)
            ? rid.GetString()
            : doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;

        if (string.IsNullOrEmpty(runId))
            throw new InvalidOperationException("Hermes run response missing run_id");

        return runId;
    }

    public Task StopRunAsync(string runId, CancellationToken cancellationToken) =>
        _http.PostAsync($"v1/runs/{runId}/stop", null, cancellationToken);

    public async Task ResolveApprovalAsync(
        string runId,
        ApprovalResolution resolution,
        CancellationToken cancellationToken)
    {
        var choice = resolution.Scope switch
        {
            ApprovalScope.Once => "once",
            ApprovalScope.Task => "once",
            ApprovalScope.Session => "session",
            ApprovalScope.Deny => "deny",
            _ => "once",
        };

        var body = new { choice, all = resolution.ResolveAll };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"v1/runs/{runId}/approval", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async IAsyncEnumerable<NormalizedAgentEvent> StreamEventsAsync(
        string runId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"v1/runs/{runId}/events");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line["data:".Length..].Trim();
            if (data == "null" || string.IsNullOrEmpty(data))
                continue;

            string eventType = "hermes.event";
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("event", out var ev))
                    eventType = ev.GetString() ?? eventType;
                else if (doc.RootElement.TryGetProperty("type", out var ty))
                    eventType = ty.GetString() ?? eventType;
            }
            catch
            {
                // keep raw payload
            }

            yield return new NormalizedAgentEvent(eventType, data, DateTimeOffset.UtcNow);
        }
    }
}
