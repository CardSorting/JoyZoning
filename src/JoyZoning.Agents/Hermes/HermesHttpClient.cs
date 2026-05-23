using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Logging;

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
        var snapshot = _settings.GetSnapshot();
        _http.BaseAddress = snapshot.ApiUri;
        _http.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrEmpty(snapshot.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.ApiKey);
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

    /// <summary>
    /// Verifies Bearer auth against the gateway (health alone does not require a key).
    /// Returns false on 401 invalid_api_key — usually means the running gateway was started
    /// with a different API_SERVER_KEY than the active JoyZoning profile.
    /// </summary>
    public async Task<bool> VerifyApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GetSnapshot().ApiKey))
            return false;

        try
        {
            using var response = await SendWithAuthRetryAsync(
                () => _http.PostAsync(
                    "v1/runs",
                    new StringContent(
                        JsonSerializer.Serialize(new { input = "joyzoning-auth-probe" }),
                        Encoding.UTF8,
                        "application/json"),
                    cancellationToken),
                cancellationToken);
            return response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK;
        }
        catch (HermesApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hermes API key probe failed");
            return false;
        }
    }

    public async Task<string> StartRunAsync(AgentRunRequest request, CancellationToken cancellationToken)
    {
        var result = await StartRunDetailedAsync(request, cancellationToken);
        return result.RunId;
    }

    public async Task<AgentRunStartResult> StartRunDetailedAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithAuthRetryAsync(
            () => _http.SendAsync(BuildRunRequest(request), cancellationToken),
            cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var runId = doc.RootElement.TryGetProperty("run_id", out var rid)
            ? rid.GetString()
            : doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;

        if (string.IsNullOrEmpty(runId))
            throw new InvalidOperationException("Hermes run response missing run_id");

        var sessionId = request.SessionId;
        if (response.Headers.TryGetValues("X-Hermes-Session-Id", out var sessionHeaders))
            sessionId = sessionHeaders.FirstOrDefault() ?? sessionId;
        if (response.Headers.TryGetValues("X-Hermes-Session-Key", out var keyHeaders) && string.IsNullOrEmpty(sessionId))
            sessionId = keyHeaders.FirstOrDefault();

        return new AgentRunStartResult(runId, sessionId);
    }

    private static HttpRequestMessage BuildRunRequest(AgentRunRequest request)
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

        var message = new HttpRequestMessage(HttpMethod.Post, "v1/runs")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrWhiteSpace(request.SessionId))
            message.Headers.TryAddWithoutValidation("X-Hermes-Session-Id", request.SessionId);

        return message;
    }

    public async Task<AgentRunPollResult?> PollRunStatusAsync(string runId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendWithAuthRetryAsync(
                () => _http.GetAsync($"v1/runs/{runId}", cancellationToken),
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.TryGetProperty("status", out var st)
                ? st.GetString() ?? "unknown"
                : "unknown";

            var terminal = status is "completed" or "failed" or "cancelled" or "stopped";
            return new AgentRunPollResult(runId, status, terminal);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hermes poll run {RunId} failed", runId);
            return null;
        }
    }

    public async Task StopRunAsync(string runId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendWithAuthRetryAsync(
                () => _http.PostAsync($"v1/runs/{runId}/stop", null, cancellationToken),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Hermes stop run {RunId} returned {Status}: {Body}",
                    runId, (int)response.StatusCode, Truncate(body, 256));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop Hermes run {RunId}", runId);
        }
    }

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
        using var response = await SendWithAuthRetryAsync(
            () => _http.PostAsync($"v1/runs/{runId}/approval", content, cancellationToken),
            cancellationToken);
    }

    public async IAsyncEnumerable<NormalizedAgentEvent> StreamEventsAsync(
        string runId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"v1/runs/{runId}/events");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await SendWithAuthRetryAsync(
            () => _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken),
            cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? pendingEventType = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;

            if (line.Length == 0 || line[0] == ':')
                continue;

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                pendingEventType = line["event:".Length..].Trim();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line["data:".Length..].Trim();
            if (data == "null" || string.IsNullOrEmpty(data))
                continue;

            yield return ParseSseEvent(data, pendingEventType);
            pendingEventType = null;
        }
    }

    private static NormalizedAgentEvent ParseSseEvent(string data, string? sseEventName)
    {
        var eventType = sseEventName ?? "hermes.event";
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;

        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("event", out var ev))
                eventType = ev.GetString() ?? eventType;
            else if (doc.RootElement.TryGetProperty("type", out var ty))
                eventType = ty.GetString() ?? eventType;

            if (doc.RootElement.TryGetProperty("timestamp", out var ts))
            {
                if (ts.ValueKind == JsonValueKind.Number && ts.TryGetDouble(out var unix))
                    occurredAt = DateTimeOffset.FromUnixTimeSeconds((long)unix);
                else if (ts.ValueKind == JsonValueKind.String &&
                         DateTimeOffset.TryParse(ts.GetString(), out var parsed))
                    occurredAt = parsed;
            }
        }
        catch
        {
            // keep defaults
        }

        return new NormalizedAgentEvent(eventType, data, occurredAt);
    }

    private async Task<HttpResponseMessage> SendWithAuthRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        var response = await send();
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            await EnsureSuccessOrThrowAsync(response, cancellationToken);
            return response;
        }

        response.Dispose();
        if (!_settings.ReloadApiKeyFromProfile())
            throw new HermesApiException(HttpStatusCode.Unauthorized, "Hermes API returned 401 and no API key is configured.");

        RefreshConnection();
        _logger.LogInformation("Reloaded Hermes API key from profile; retrying request once.");

        response = await send();
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return response;
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HermesApiException(
            response.StatusCode,
            $"Hermes API error {(int)response.StatusCode}: {Truncate(body, 512)}",
            body);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
