using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

/// <summary>HTTP client for the JoyZoning control plane REST API.</summary>
public sealed class JoyZoningCliClient : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly HttpClient _http;

    public JoyZoningCliClient(string baseUrl, TimeSpan? timeout = null)
    {
        var normalized = baseUrl.TrimEnd('/') + "/";
        _http = new HttpClient { BaseAddress = new Uri(normalized), Timeout = timeout ?? TimeSpan.FromSeconds(120) };
    }

    public Task<CliHttpResult> HealthAsync() => GetAsync("api/health");

    public Task<CliHttpResult> BroccoliQHealthAsync() => GetAsync("api/broccoliq/health");

    public Task<CliHttpResult> BroccoliQFlushAsync() => PostEmptyAsync("api/broccoliq/flush");

    public Task<CliHttpResult> BroccoliQBackfillAsync(int? maxEvents = null)
    {
        var path = maxEvents.HasValue
            ? $"api/broccoliq/backfill?maxEvents={maxEvents.Value}"
            : "api/broccoliq/backfill";
        return PostEmptyAsync(path);
    }

    public Task<CliHttpResult> BroccoliQAuditAsync(int limit = 50) =>
        GetAsync($"api/broccoliq/audit?limit={limit}");

    public Task<CliHttpResult> BroccoliQTasksAsync(int limit = 50, Guid? taskId = null)
    {
        var path = taskId.HasValue
            ? $"api/broccoliq/tasks?limit={limit}&taskId={taskId.Value}"
            : $"api/broccoliq/tasks?limit={limit}";
        return GetAsync(path);
    }

    public Task<CliHttpResult> ListSessionsAsync() => GetAsync("api/sessions");

    public Task<CliHttpResult> GetSessionAsync(Guid id) => GetAsync($"api/sessions/{id}");

    public Task<CliHttpResult> GetDeliveryPlanAsync(Guid sessionId) =>
        GetAsync($"api/sessions/{sessionId}/delivery-plan");

    public Task<CliHttpResult> CreateDeliveryChainAsync(string programName, string workspaceRoot) =>
        PostJsonAsync("api/delivery-chains", new { programName, workspaceRoot });

    public Task<CliHttpResult> GetDeliveryChainQueueAsync(Guid chainId) =>
        GetAsync($"api/delivery-chains/{chainId}/queue");

    public Task<CliHttpResult> DeliveryChainNextExternalAsync(Guid chainId, string agent) =>
        PostJsonAsync($"api/delivery-chains/{chainId}/next-external", new { agent });

    public Task<CliHttpResult> DeliveryChainPromptAsync(Guid chainId) =>
        GetAsync($"api/delivery-chains/{chainId}/prompt");

    public Task<CliHttpResult> StartExternalTaskAsync(Guid taskId, string agent) =>
        PostJsonAsync($"api/tasks/{taskId}/external/start", new { agent });

    public Task<CliHttpResult> GetExternalTaskPromptAsync(Guid taskId) =>
        GetAsync($"api/tasks/{taskId}/external/prompt");

    public Task<CliHttpResult> GetExternalTaskStatusAsync(Guid taskId, bool refresh = false) =>
        GetAsync($"api/tasks/{taskId}/external/status?refresh={refresh.ToString().ToLowerInvariant()}");

    public Task<CliHttpResult> GetTaskWorkspaceStatusAsync(Guid taskId) =>
        GetAsync($"api/tasks/{taskId}/workspace/status");

    public Task<CliHttpResult> MarkExternalReadyForReviewAsync(Guid taskId) =>
        PostEmptyAsync($"api/tasks/{taskId}/external/ready-for-review");

    public Task<CliHttpResult> CompleteExternalTaskAsync(Guid taskId, bool operatorApproved, bool alreadyMerged = false) =>
        PostJsonAsync($"api/tasks/{taskId}/external/complete", new { operatorApproved, alreadyMerged });

    public Task<CliHttpResult> CreateSessionAsync(string name, string workspaceRoot, string? hermesProfile) =>
        PostJsonAsync("api/sessions", new { name, workspaceRoot, hermesProfile });

    public Task<CliHttpResult> ListTasksAsync(Guid sessionId) =>
        GetAsync($"api/tasks?sessionId={sessionId}");

    public Task<CliHttpResult> GetTaskAsync(Guid taskId) => GetAsync($"api/tasks/{taskId}");

    public Task<CliHttpResult> CreateTaskAsync(
        Guid sessionId,
        string title,
        string? description,
        AgentKind agent,
        RiskLevel risk) =>
        PostJsonAsync("api/tasks", new
        {
            sessionId,
            title,
            description,
            assignedAgent = (int)agent,
            risk = (int)risk,
        });

    public Task<CliHttpResult> UpdateTaskStatusAsync(Guid taskId, WorkTaskStatus status, StatusChangeActor actor) =>
        PutJsonAsync($"api/tasks/{taskId}/status", new { status = (int)status, actor = (int)actor });

    public Task<CliHttpResult> DispatchTaskAsync(Guid taskId, bool humanApprovedCritical) =>
        PostJsonAsync($"api/tasks/{taskId}/dispatch", new { humanApprovedCritical });

    public Task<CliHttpResult> GetLeaseAsync(Guid taskId) => GetAsync($"api/tasks/{taskId}/lease");

    public Task<CliHttpResult> HeartbeatLeaseAsync(Guid taskId, Guid sessionId, StatusChangeActor actor) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/heartbeat", new { sessionId, actor = (int)actor });

    public Task<CliHttpResult> RecoverLeaseAsync(
        Guid taskId,
        Guid sessionId,
        LeaseRecoveryMode mode,
        StatusChangeActor actor,
        bool humanApprovedCritical) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/recover", new
        {
            sessionId,
            mode = (int)mode,
            actor = (int)actor,
            humanApprovedCritical,
        });

    public Task<CliHttpResult> DispatchRetryAsync(
        Guid taskId,
        Guid sessionId,
        StatusChangeActor actor,
        bool humanApprovedCritical) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/dispatch-retry", new
        {
            sessionId,
            actor = (int)actor,
            humanApprovedCritical,
        });

    public Task<CliHttpResult> AgentLeaseStatusAsync(
        Guid taskId,
        ExecutionLeaseStatus status,
        string? reason) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/agent-status", new { status = (int)status, reason });

    public Task<CliHttpResult> SubmitVerificationAsync(
        Guid taskId,
        VerificationReport report,
        bool supersede) =>
        PostJsonAsync($"api/tasks/{taskId}/verification", new { report, supersede });

    public Task<CliHttpResult> RevokeLeaseAsync(Guid taskId, string? reason) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/revoke", new { reason });

    public Task<CliHttpResult> MergeLeaseAsync(Guid taskId) =>
        PostEmptyAsync($"api/tasks/{taskId}/lease/merge");

    public Task<CliHttpResult> FailLeaseAsync(Guid taskId, string reason) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/fail", new { reason });

    public Task<CliHttpResult> RecordAgentEvidenceAsync(
        Guid taskId,
        string kind,
        string summary,
        object? detail = null) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/agent-evidence", new { kind, summary, detail });

    public Task<CliHttpResult> ImportKanbanAsync(Guid sessionId) =>
        PostJsonAsync("api/tasks/import-kanban", new { sessionId });

    public Task<CliHttpResult> GetExecutionAsync(Guid id) => GetAsync($"api/executions/{id}");

    public Task<CliHttpResult> ListInterruptedExecutionsAsync() => GetAsync("api/executions/interrupted");

    public Task<CliHttpResult> ResumeExecutionAsync(Guid id) => PostEmptyAsync($"api/executions/{id}/resume");

    public Task<CliHttpResult> CancelExecutionAsync(Guid id) => PostEmptyAsync($"api/executions/{id}/cancel");

    public Task<CliHttpResult> ListPendingApprovalsAsync() => GetAsync("api/approvals/pending");

    public Task<CliHttpResult> ResolveApprovalAsync(Guid id, ApprovalScope scope) =>
        PostJsonAsync($"api/approvals/{id}/resolve", new { scope = (int)scope });

    public Task<CliHttpResult> SendManagerMessageAsync(Guid sessionId, string message) =>
        PostJsonAsync("api/manager/message", new { sessionId, message });

    public Task<CliHttpResult> ListEventsAsync(long? since, Guid? correlationId, string? types) =>
        GetAsync(BuildQuery("api/events", new Dictionary<string, string?>
        {
            ["since"] = since?.ToString(),
            ["correlationId"] = correlationId?.ToString(),
            ["types"] = types,
        }));

    public Task<CliHttpResult> WorkspaceTreeAsync(string workspaceRoot) =>
        GetAsync($"api/workspace/tree?workspaceRoot={Uri.EscapeDataString(workspaceRoot)}");

    public Task<CliHttpResult> WorkspaceChangedAsync(string workspaceRoot, Guid? sessionId = null)
    {
        var q = $"api/workspace/changed?workspaceRoot={Uri.EscapeDataString(workspaceRoot)}";
        if (sessionId is { } sid && sid != Guid.Empty)
            q += $"&sessionId={sid}";
        return GetAsync(q);
    }

    public Task<CliHttpResult> GetTaskWorkspaceChangedAsync(Guid taskId, Guid? sessionId = null)
    {
        var q = $"api/tasks/{taskId}/workspace/changed";
        if (sessionId is { } sid && sid != Guid.Empty)
            q += $"?sessionId={sid}";
        return GetAsync(q);
    }

    public Task<CliHttpResult> GetTaskWorkspaceTreeAsync(Guid taskId) =>
        GetAsync($"api/tasks/{taskId}/workspace/tree");

    public Task<CliHttpResult> GetTaskWorkspaceDiffAsync(Guid taskId, string path) =>
        GetAsync($"api/tasks/{taskId}/workspace/diff?path={Uri.EscapeDataString(path)}");

    public Task<CliHttpResult> WorkspaceDiffAsync(string workspaceRoot, string path) =>
        GetAsync($"api/workspace/diff?workspaceRoot={Uri.EscapeDataString(workspaceRoot)}&path={Uri.EscapeDataString(path)}");

    public Task<CliHttpResult> HermesHealthAsync() => GetAsync("api/hermes/health");

    public Task<CliHttpResult> HermesEnsureAsync() => PostEmptyAsync("api/hermes/ensure");

    public Task<CliHttpResult> HermesDashboardAsync() => GetAsync("api/hermes/dashboard");

    public Task<CliHttpResult> HermesEnsureDashboardAsync(bool alsoEnsureGateway) =>
        PostJsonAsync("api/hermes/ensure-dashboard", new { alsoEnsureGateway });

    public Task<CliHttpResult> HermesRefreshDashboardTokenAsync() =>
        PostEmptyAsync("api/hermes/refresh-dashboard-token");

    public Task<CliHttpResult> HermesConnectorStatusAsync() =>
        GetAsync("api/hermes/connector-status");

    public Task<CliHttpResult> GetConfigAsync() => GetAsync("api/config");

    public Task<CliHttpResult> SaveConfigAsync(object settings) =>
        PutJsonAsync("api/config", settings);

    public Task<CliHttpResult> KanbanSyncStatusAsync() => GetAsync("api/kanban/sync-status");

    public Task<CliHttpResult> RawAsync(HttpMethod method, string path, string? jsonBody)
    {
        path = path.TrimStart('/');
        if (method == HttpMethod.Get || method == HttpMethod.Delete)
            return method == HttpMethod.Get ? GetAsync(path) : DeleteAsync(path);

        if (jsonBody is null)
            return PostEmptyAsync(path);

        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return SendAsync(() => _http.PostAsync(path, content));
    }

    public void Dispose() => _http.Dispose();

    private Task<CliHttpResult> GetAsync(string path) => SendAsync(() => _http.GetAsync(path));

    private Task<CliHttpResult> DeleteAsync(string path) => SendAsync(() => _http.DeleteAsync(path));

    private Task<CliHttpResult> PostEmptyAsync(string path) =>
        SendAsync(() => _http.PostAsync(path, null));

    private Task<CliHttpResult> PutJsonAsync(string path, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        return SendAsync(() => _http.PutAsync(path, content));
    }

    private Task<CliHttpResult> PostJsonAsync(string path, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        return SendAsync(() => _http.PostAsync(path, content));
    }

    private async Task<CliHttpResult> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            var response = await send();
            var text = await response.Content.ReadAsStringAsync();
            return CliHttpResult.FromResponse(response.StatusCode, text);
        }
        catch (Exception ex)
        {
            return CliHttpResult.NetworkError(ex.Message);
        }
    }

    private static string BuildQuery(string path, Dictionary<string, string?> parameters)
    {
        var parts = parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}")
            .ToList();
        return parts.Count == 0 ? path : $"{path}?{string.Join("&", parts)}";
    }
}

public sealed record CliHttpResult(
    HttpStatusCode StatusCode,
    string RawText,
    JsonElement? Body,
    string? Error,
    string? Message,
    bool IsNetworkError)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300 && !IsNetworkError;

    public static CliHttpResult FromResponse(HttpStatusCode status, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new(status, text, null, null, null, false);

        try
        {
            var body = JsonSerializer.Deserialize<JsonElement>(text, JoyZoningCliClient.JsonOptions);
            string? error = null;
            string? message = null;
            if (body.ValueKind == JsonValueKind.Object)
            {
                if (body.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                    error = e.GetString();
                if (body.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    message = m.GetString();
            }

            if (error is null && (int)status is 401 or 403)
                error = "unauthorized";

            return new(status, text, body, error, message, false);
        }
        catch
        {
            return new(status, text, null, null, null, false);
        }
    }

    public static CliHttpResult NetworkError(string message) =>
        new(HttpStatusCode.ServiceUnavailable, message, null, "network_error", message, true);
}
