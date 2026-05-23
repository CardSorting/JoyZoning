using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.App.Services;

public class ControlPlaneClient
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://127.0.0.1:9470/"),
        Timeout = TimeSpan.FromSeconds(30),
    };

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var r = await _http.GetAsync("api/health");
            return r.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<HermesHealthResult?> GetHermesHealthAsync()
    {
        try
        {
            var r = await _http.GetAsync("api/hermes/health");
            if (!r.IsSuccessStatusCode) return null;
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            return new HermesHealthResult(
                json.GetProperty("state").GetString() ?? "Unknown",
                json.GetProperty("message").GetString() ?? "");
        }
        catch
        {
            return null;
        }
    }

    public async Task<BroccoliQHealthResult?> GetBroccoliQHealthAsync()
    {
        try
        {
            var r = await _http.GetAsync("api/broccoliq/health");
            if (!r.IsSuccessStatusCode) return null;
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            var mirrorDropped = 0L;
            if (json.TryGetProperty("mirror", out var mirror) &&
                mirror.TryGetProperty("dropped", out var dropped))
                mirrorDropped = dropped.GetInt64();

            return new BroccoliQHealthResult(
                json.TryGetProperty("status", out var st) ? st.GetString() ?? "unknown" : "unknown",
                json.TryGetProperty("enabled", out var en) && en.GetBoolean(),
                json.TryGetProperty("bridgeReady", out var ready) && ready.GetBoolean(),
                json.TryGetProperty("message", out var msg) ? msg.GetString() : null,
                mirrorDropped);
        }
        catch
        {
            return null;
        }
    }

    public async Task<HermesHealthResult?> EnsureHermesAsync()
    {
        try
        {
            var r = await _http.PostAsync("api/hermes/ensure", null);
            if (!r.IsSuccessStatusCode) return null;
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            return new HermesHealthResult(
                json.GetProperty("state").GetString() ?? "Unknown",
                json.GetProperty("message").GetString() ?? "");
        }
        catch
        {
            return null;
        }
    }

    public async Task<DashboardStatus?> GetDashboardStatusAsync()
    {
        try
        {
            var r = await _http.GetAsync("api/hermes/dashboard");
            if (!r.IsSuccessStatusCode) return null;
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            return new DashboardStatus(
                json.GetProperty("reachable").GetBoolean(),
                json.GetProperty("hasToken").GetBoolean(),
                json.GetProperty("tokenValid").GetBoolean(),
                json.GetProperty("hermesCliFound").GetBoolean(),
                json.GetProperty("ready").GetBoolean(),
                json.GetProperty("dashboardUrl").GetString() ?? "",
                json.GetProperty("message").GetString() ?? "");
        }
        catch
        {
            return null;
        }
    }

    public async Task<DashboardEnsureResult?> EnsureDashboardAsync(bool alsoEnsureGateway = true)
    {
        try
        {
            var body = new { alsoEnsureGateway };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var r = await _http.PostAsync("api/hermes/ensure-dashboard", content);
            if (!r.IsSuccessStatusCode) return null;
            return ParseDashboardEnsure(await r.Content.ReadFromJsonAsync<JsonElement>());
        }
        catch
        {
            return null;
        }
    }

    public async Task<DashboardEnsureResult?> RefreshDashboardTokenAsync()
    {
        try
        {
            var r = await _http.PostAsync("api/hermes/refresh-dashboard-token", null);
            if (!r.IsSuccessStatusCode) return null;
            return ParseDashboardEnsure(await r.Content.ReadFromJsonAsync<JsonElement>());
        }
        catch
        {
            return null;
        }
    }

    private static DashboardEnsureResult ParseDashboardEnsure(JsonElement json)
    {
        var steps = new List<DashboardStep>();
        if (json.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in stepsEl.EnumerateArray())
            {
                steps.Add(new DashboardStep(
                    s.GetProperty("id").GetString() ?? "",
                    s.GetProperty("label").GetString() ?? "",
                    s.GetProperty("status").GetString() ?? "",
                    s.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String
                        ? d.GetString()
                        : null));
            }
        }

        return new DashboardEnsureResult(
            json.GetProperty("reachable").GetBoolean(),
            json.GetProperty("tokenAcquired").GetBoolean(),
            json.GetProperty("tokenValid").GetBoolean(),
            json.GetProperty("ready").GetBoolean(),
            json.GetProperty("message").GetString() ?? "",
            steps);
    }

    public async Task<IReadOnlyList<SessionInfo>> ListSessionsAsync()
    {
        var r = await _http.GetAsync("api/sessions");
        if (!r.IsSuccessStatusCode) return Array.Empty<SessionInfo>();

        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (json.ValueKind != JsonValueKind.Array) return Array.Empty<SessionInfo>();

        return json.EnumerateArray().Select(ParseSession).Where(s => s is not null).Cast<SessionInfo>().ToList();
    }

    public async Task<SessionInfo?> CreateSessionAsync(string name, string workspaceRoot)
    {
        var body = new { name, workspaceRoot, hermesProfile = "joyzoning" };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await _http.PostAsync("api/sessions", content);
        if (!r.IsSuccessStatusCode) return null;
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        return ParseSession(json);
    }

    public async Task<IReadOnlyList<TaskInfo>> ListTasksAsync(Guid sessionId)
    {
        var r = await _http.GetAsync($"api/tasks?sessionId={sessionId}");
        if (!r.IsSuccessStatusCode) return Array.Empty<TaskInfo>();

        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (json.ValueKind != JsonValueKind.Array) return Array.Empty<TaskInfo>();

        return json.EnumerateArray().Select(ParseTask).Where(t => t is not null).Cast<TaskInfo>().ToList();
    }

    public Task<ApiCallResult<TaskInfo>> UpdateTaskStatusDetailedAsync(
        Guid taskId,
        WorkTaskStatus status,
        StatusChangeActor actor = StatusChangeActor.Human) =>
        SendAsync<TaskInfo>(
            () => PutJsonAsync($"api/tasks/{taskId}/status", new { status = (int)status, actor = (int)actor }),
            ParseTask);

    public async Task<bool> UpdateTaskStatusAsync(Guid taskId, WorkTaskStatus status)
    {
        var result = await UpdateTaskStatusDetailedAsync(taskId, status);
        return result.Success;
    }

    public Task<ApiCallResult<JsonElement>> DispatchTaskDetailedAsync(
        Guid taskId,
        DispatchOptions? options = null) =>
        SendAsync<JsonElement>(
            () => PostJsonAsync(
                $"api/tasks/{taskId}/dispatch",
                new { humanApprovedCritical = options?.HumanApprovedCritical ?? false }),
            el => el);

    public async Task<bool> DispatchTaskAsync(Guid taskId, bool humanApprovedCritical = false)
    {
        var result = await DispatchTaskDetailedAsync(taskId, new DispatchOptions(humanApprovedCritical));
        return result.Success;
    }

    public Task<ApiCallResult<LeaseInfo>> GetLeaseDetailedAsync(Guid taskId) =>
        SendAsync<LeaseInfo>(
            () => _http.GetAsync($"api/tasks/{taskId}/lease"),
            ParseLease);

    public Task<ApiCallResult<LeaseInfo>> SubmitAgentLeaseStatusAsync(
        Guid taskId,
        AgentLeaseStatusRequest request) =>
        SendAsync<LeaseInfo>(
            () => PostJsonAsync(
                $"api/tasks/{taskId}/lease/agent-status",
                new { status = (int)request.Status, reason = request.Reason }),
            ParseLease);

    public Task<ApiCallResult<LeaseInfo>> SubmitVerificationDetailedAsync(
        Guid taskId,
        SubmitVerificationRequest request) =>
        SendAsync<LeaseInfo>(
            () => PostJsonAsync(
                $"api/tasks/{taskId}/verification",
                new { report = request.Report, supersede = request.Supersede }),
            ParseLease);

    public Task<ApiCallResult<LeaseInfo>> RevokeLeaseDetailedAsync(Guid taskId, RevokeLeaseRequest? request = null) =>
        SendAsync<LeaseInfo>(
            () => PostJsonAsync($"api/tasks/{taskId}/lease/revoke", new { reason = request?.Reason }),
            ParseLease);

    public Task<ApiCallResult<TaskInfo>> MergeLeaseDetailedAsync(Guid taskId) =>
        SendAsync<TaskInfo>(
            () => _http.PostAsync($"api/tasks/{taskId}/lease/merge", null),
            ParseTask);

    public async Task<KanbanImportResult?> ImportKanbanAsync(Guid sessionId)
    {
        var body = new { sessionId };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await _http.PostAsync("api/tasks/import-kanban", content);
        if (!r.IsSuccessStatusCode) return null;
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        var pushed = json.TryGetProperty("pushed", out var p) ? p.GetInt32() : 0;
        return new KanbanImportResult(
            json.GetProperty("imported").GetInt32(),
            json.GetProperty("updated").GetInt32(),
            json.GetProperty("skipped").GetInt32(),
            pushed,
            json.GetProperty("message").GetString() ?? "");
    }

    public async Task<TaskInfo?> CreateTaskAsync(
        Guid sessionId,
        string title,
        string? description,
        AgentKind agent = AgentKind.DietCode)
    {
        var body = new { sessionId, title, description, assignedAgent = (int)agent, risk = 0 };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await _http.PostAsync("api/tasks", content);
        if (!r.IsSuccessStatusCode) return null;
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        return ParseTask(json);
    }

    public async Task<AppSettings?> GetSettingsAsync()
    {
        var r = await _http.GetAsync("api/config");
        if (!r.IsSuccessStatusCode) return null;
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        return new AppSettings(
            json.GetProperty("installRoot").GetString() ?? "",
            json.GetProperty("apiBaseUrl").GetString() ?? "",
            json.GetProperty("dashboardBaseUrl").GetString() ?? "",
            json.GetProperty("profile").GetString() ?? "",
            json.GetProperty("dashboardSessionToken").GetString() ?? "",
            json.GetProperty("dashboardReachable").GetBoolean(),
            json.TryGetProperty("autoSyncEnabled", out var sync) && sync.GetBoolean(),
            json.TryGetProperty("autoSyncIntervalSeconds", out var interval)
                ? interval.GetInt32()
                : 120);
    }

    public async Task<KanbanSyncStatus?> GetKanbanSyncStatusAsync()
    {
        try
        {
            var r = await _http.GetAsync("api/kanban/sync-status");
            if (!r.IsSuccessStatusCode) return null;
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            DateTimeOffset? at = null;
            if (json.TryGetProperty("lastSyncAt", out var la) && la.ValueKind == JsonValueKind.String)
                at = DateTimeOffset.Parse(la.GetString()!);
            return new KanbanSyncStatus(
                at,
                json.GetProperty("message").GetString() ?? "");
        }
        catch
        {
            return null;
        }
    }

    public async Task<AppSettings?> SaveSettingsAsync(AppSettings settings)
    {
        var payload = new
        {
            installRoot = settings.InstallRoot,
            apiBaseUrl = settings.ApiBaseUrl,
            dashboardBaseUrl = settings.DashboardBaseUrl,
            profile = settings.Profile,
            dashboardSessionToken = settings.DashboardSessionToken,
            dashboardReachable = settings.DashboardReachable,
            autoSyncEnabled = settings.AutoSyncEnabled,
            autoSyncIntervalSeconds = settings.AutoSyncIntervalSeconds,
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var r = await _http.PutAsync("api/config", content);
        if (!r.IsSuccessStatusCode) return null;
        return await GetSettingsAsync();
    }

    public async Task<IReadOnlyList<InterruptedExecution>> ListInterruptedExecutionsAsync()
    {
        var r = await _http.GetAsync("api/executions/interrupted");
        if (!r.IsSuccessStatusCode) return Array.Empty<InterruptedExecution>();

        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (json.ValueKind != JsonValueKind.Array) return Array.Empty<InterruptedExecution>();

        return json.EnumerateArray().Select(e => new InterruptedExecution(
            Guid.Parse(e.GetProperty("id").GetString()!),
            Guid.Parse(e.GetProperty("workTaskId").GetString()!),
            e.GetProperty("objective").GetString() ?? "",
            e.GetProperty("hermesRunId").GetString() ?? "")).ToList();
    }

    public async Task<bool> ResumeExecutionAsync(Guid executionId)
    {
        var r = await _http.PostAsync($"api/executions/{executionId}/resume", null);
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> CancelExecutionAsync(Guid executionId)
    {
        var r = await _http.PostAsync($"api/executions/{executionId}/cancel", null);
        return r.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<ApprovalInfo>> ListPendingApprovalsAsync()
    {
        var r = await _http.GetAsync("api/approvals/pending");
        if (!r.IsSuccessStatusCode) return Array.Empty<ApprovalInfo>();

        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (json.ValueKind != JsonValueKind.Array) return Array.Empty<ApprovalInfo>();

        return json.EnumerateArray().Select(ParseApproval).Where(a => a is not null).Cast<ApprovalInfo>().ToList();
    }

    public async Task<bool> ResolveApprovalAsync(Guid approvalId, ApprovalScope scope)
    {
        var body = new { scope = (int)scope };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await _http.PostAsync($"api/approvals/{approvalId}/resolve", content);
        return r.IsSuccessStatusCode;
    }

    public async Task<ManagerMessageResult?> SendManagerMessageAsync(Guid sessionId, string message)
    {
        var body = new { sessionId, message };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var r = await _http.PostAsync("api/manager/message", content);
        if (!r.IsSuccessStatusCode) return null;
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        var runId = json.TryGetProperty("runId", out var rid) ? rid.GetString() : null;
        return new ManagerMessageResult(runId ?? "");
    }

    public async Task<IReadOnlyList<EventInfo>> ListEventsAsync(Guid? correlationId = null, long? since = null)
    {
        var qs = new List<string>();
        if (correlationId.HasValue) qs.Add($"correlationId={correlationId}");
        if (since.HasValue) qs.Add($"since={since}");
        var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";

        var r = await _http.GetAsync($"api/events{query}");
        if (!r.IsSuccessStatusCode) return Array.Empty<EventInfo>();

        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (json.ValueKind != JsonValueKind.Array) return Array.Empty<EventInfo>();

        return json.EnumerateArray().Select(ParseEvent).Where(e => e is not null).Cast<EventInfo>().ToList();
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        Func<JsonElement, T?> parse)
    {
        try
        {
            var response = await send();
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                var error = ParseError(response.StatusCode, text);
                return ApiCallResult<T>.Fail(response.StatusCode, error);
            }

            if (string.IsNullOrWhiteSpace(text))
                return ApiCallResult<T>.Fail(response.StatusCode,
                    new ApiErrorResponse("empty_response", "Empty response from control plane."));

            var json = JsonSerializer.Deserialize<JsonElement>(text);
            var data = parse(json);
            if (data is null)
                return ApiCallResult<T>.Fail(response.StatusCode,
                    new ApiErrorResponse("parse_error", "Could not parse control plane response."));

            return ApiCallResult<T>.Ok(data, response.StatusCode);
        }
        catch (Exception ex)
        {
            return ApiCallResult<T>.Fail(HttpStatusCode.ServiceUnavailable,
                new ApiErrorResponse("network_error", ex.Message, "Check that the control plane is running."));
        }
    }

    private Task<HttpResponseMessage> PostJsonAsync(string path, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return _http.PostAsync(path, content);
    }

    private Task<HttpResponseMessage> PutJsonAsync(string path, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return _http.PutAsync(path, content);
    }

    private static ApiErrorResponse ParseError(HttpStatusCode status, string text)
    {
        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(text);
            if (json.ValueKind == JsonValueKind.Object)
            {
                var code = json.TryGetProperty("error", out var e) ? e.GetString() ?? "api_error" : "api_error";
                var message = json.TryGetProperty("message", out var m)
                    ? m.GetString() ?? "Request failed."
                    : "Request failed.";
                var next = OperatorApiHints.NextActionFor(code, message);
                return new ApiErrorResponse(code, message, next);
            }
        }
        catch
        {
            // fall through
        }

        return new ApiErrorResponse(
            "api_error",
            $"Request failed ({(int)status}).",
            OperatorApiHints.NextActionFor("api_error", null));
    }

    private static LeaseInfo? ParseLease(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var id)) return null;
        return new LeaseInfo(
            Guid.Parse(id.GetString()!),
            Guid.Parse(el.GetProperty("workTaskId").GetString()!),
            (ExecutionLeaseStatus)el.GetProperty("status").GetInt32(),
            el.GetProperty("worktreePath").GetString() ?? "",
            el.GetProperty("branchName").GetString() ?? "",
            el.TryGetProperty("criticalApprovalGranted", out var g) && g.GetBoolean(),
            el.TryGetProperty("criticalApprovalConsumed", out var c) && c.GetBoolean(),
            el.TryGetProperty("blockedReason", out var br) && br.ValueKind == JsonValueKind.String
                ? br.GetString()
                : null);
    }

    private static SessionInfo? ParseSession(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var id)) return null;
        return new SessionInfo(
            Guid.Parse(id.GetString()!),
            el.GetProperty("name").GetString() ?? "",
            el.GetProperty("workspaceRoot").GetString() ?? "");
    }

    private static TaskInfo? ParseTask(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var id)) return null;
        return new TaskInfo(
            Guid.Parse(id.GetString()!),
            el.GetProperty("title").GetString() ?? "",
            el.GetProperty("description").GetString() ?? "",
            (WorkTaskStatus)el.GetProperty("status").GetInt32(),
            el.GetProperty("assignedAgent").GetInt32() == 0 ? AgentKind.Hermes : AgentKind.DietCode,
            (RiskLevel)el.GetProperty("risk").GetInt32());
    }

    private static ApprovalInfo? ParseApproval(JsonElement el)
    {
        if (!el.TryGetProperty("id", out var id)) return null;
        return new ApprovalInfo(
            Guid.Parse(id.GetString()!),
            el.GetProperty("command").GetString() ?? "",
            el.GetProperty("description").GetString() ?? "",
            el.GetProperty("risk").GetInt32().ToString());
    }

    private static EventInfo? ParseEvent(JsonElement el) =>
        new(
            el.GetProperty("id").GetInt64(),
            el.GetProperty("type").GetString() ?? "",
            el.GetProperty("source").GetInt32().ToString(),
            el.GetProperty("payloadJson").GetString() ?? "{}",
            DateTimeOffset.Parse(el.GetProperty("occurredAt").GetString()!));

    public record HermesHealthResult(string State, string Message);

    public record BroccoliQHealthResult(
        string Status,
        bool Enabled,
        bool BridgeReady,
        string? Message,
        long MirrorDropped);

    public record DashboardStatus(
        bool Reachable,
        bool HasToken,
        bool TokenValid,
        bool HermesCliFound,
        bool Ready,
        string DashboardUrl,
        string Message);

    public record DashboardStep(string Id, string Label, string Status, string? Detail);

    public record DashboardEnsureResult(
        bool Reachable,
        bool TokenAcquired,
        bool TokenValid,
        bool Ready,
        string Message,
        IReadOnlyList<DashboardStep> Steps);
    public record SessionInfo(Guid Id, string Name, string WorkspaceRoot);
    public record TaskInfo(Guid Id, string Title, string Description, WorkTaskStatus Status, AgentKind Agent, RiskLevel Risk);
    public record ApprovalInfo(Guid Id, string Command, string Description, string Risk);
    public record ManagerMessageResult(string RunId);
    public record EventInfo(long Id, string Type, string Source, string PayloadJson, DateTimeOffset OccurredAt);

    public record KanbanImportResult(int Imported, int Updated, int Skipped, int Pushed, string Message);

    public record AppSettings(
        string InstallRoot,
        string ApiBaseUrl,
        string DashboardBaseUrl,
        string Profile,
        string DashboardSessionToken,
        bool DashboardReachable,
        bool AutoSyncEnabled = false,
        int AutoSyncIntervalSeconds = 120);

    public record KanbanSyncStatus(DateTimeOffset? LastSyncAt, string Message);

    public record InterruptedExecution(Guid Id, Guid WorkTaskId, string Objective, string HermesRunId);
}
