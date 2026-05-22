using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Tests.Infrastructure;

internal sealed class OrchestrationApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public OrchestrationApiClient(HttpClient http) => _http = http;

    public Task<HttpResponseMessage> GetLeaseAsync(Guid taskId) =>
        _http.GetAsync($"api/tasks/{taskId}/lease");

    public Task<HttpResponseMessage> DispatchAsync(Guid taskId, bool humanApprovedCritical = false) =>
        PostJsonAsync($"api/tasks/{taskId}/dispatch", new { humanApprovedCritical });

    public Task<HttpResponseMessage> AgentStatusAsync(
        Guid taskId,
        ExecutionLeaseStatus status,
        string? reason = null) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/agent-status", new { status, reason });

    public Task<HttpResponseMessage> VerificationAsync(
        Guid taskId,
        VerificationReport report,
        bool supersede = false) =>
        PostJsonAsync($"api/tasks/{taskId}/verification", new { report, supersede });

    public Task<HttpResponseMessage> RevokeAsync(Guid taskId, string? reason = null) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/revoke", new { reason });

    public Task<HttpResponseMessage> MergeAsync(Guid taskId) =>
        _http.PostAsync($"api/tasks/{taskId}/lease/merge", null);

    public Task<HttpResponseMessage> HeartbeatAsync(Guid taskId, Guid sessionId) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/heartbeat", new { sessionId, actor = (int)StatusChangeActor.DietCode });

    public Task<HttpResponseMessage> RecoverAsync(
        Guid taskId,
        Guid sessionId,
        LeaseRecoveryMode mode,
        bool humanApprovedCritical = false) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/recover", new
        {
            sessionId,
            mode = (int)mode,
            actor = (int)StatusChangeActor.Human,
            humanApprovedCritical,
        });

    public Task<HttpResponseMessage> DispatchRetryAsync(
        Guid taskId,
        Guid sessionId,
        bool humanApprovedCritical) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/dispatch-retry", new
        {
            sessionId,
            actor = (int)StatusChangeActor.Human,
            humanApprovedCritical,
        });

    public Task<HttpResponseMessage> FailLeaseAsync(Guid taskId, string reason) =>
        PostJsonAsync($"api/tasks/{taskId}/lease/fail", new { reason });

    public Task<HttpResponseMessage> UpdateTaskStatusAsync(
        Guid taskId,
        WorkTaskStatus status,
        StatusChangeActor actor = StatusChangeActor.Human) =>
        PutJsonAsync($"api/tasks/{taskId}/status", new { status, actor });

    public async Task<(Guid SessionId, Guid TaskId)> SeedTaskAsync(
        string workspaceRoot,
        RiskLevel risk = RiskLevel.Low)
    {
        var sessionBody = new { name = "api-test", workspaceRoot, hermesProfile = "joyzoning" };
        var sessionResp = await PostJsonAsync("api/sessions", sessionBody);
        sessionResp.EnsureSuccessStatusCode();
        var sessionJson = await sessionResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sessionId = Guid.Parse(sessionJson.GetProperty("id").GetString()!);

        var taskBody = new
        {
            sessionId,
            title = "API test card",
            description = "Objective for orchestration API tests",
            assignedAgent = (int)AgentKind.DietCode,
            risk = (int)risk,
        };
        var taskResp = await PostJsonAsync("api/tasks", taskBody);
        taskResp.EnsureSuccessStatusCode();
        var taskJson = await taskResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var taskId = Guid.Parse(taskJson.GetProperty("id").GetString()!);
        return (sessionId, taskId);
    }

    public static async Task<(HttpStatusCode Status, JsonElement? Body, string? Error, string? Message)> ReadAsync(
        HttpResponseMessage response)
    {
        var status = response.StatusCode;
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
            return (status, null, null, null);

        var body = JsonSerializer.Deserialize<JsonElement>(text, JsonOptions);
        string? error = null;
        string? message = null;
        if (body.ValueKind == JsonValueKind.Object)
        {
            if (body.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                error = e.GetString();
            if (body.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                message = m.GetString();
        }

        return (status, body, error, message);
    }

    public static int LeaseStatus(JsonElement lease) =>
        lease.GetProperty("status").GetInt32();

    private Task<HttpResponseMessage> PostJsonAsync(string path, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        return _http.PostAsync(path, content);
    }

    private Task<HttpResponseMessage> PutJsonAsync(string path, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        return _http.PutAsync(path, content);
    }
}
