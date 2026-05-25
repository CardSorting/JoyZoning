using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Mapping;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Agents.Hermes;

/// <summary>
/// Sync to diet-hermes kanban dashboard plugin. Retries once after token refresh on 401/403.
/// </summary>
public class KanbanSyncService
{
    public const string HttpClientName = "hermes-kanban";

    private readonly IHttpClientFactory _httpFactory;
    private readonly HermesRuntimeSettings _settings;
    private readonly ILogger<KanbanSyncService> _logger;
    private readonly IDashboardTokenRefresher? _tokenRefresher;

    public KanbanSyncService(
        IHttpClientFactory httpFactory,
        HermesRuntimeSettings settings,
        ILogger<KanbanSyncService> logger,
        IDashboardTokenRefresher? tokenRefresher = null)
    {
        _httpFactory = httpFactory;
        _settings = settings;
        _logger = logger;
        _tokenRefresher = tokenRefresher;
    }

    public void SetDashboardToken(string? token) => _dashboardToken = token;

    private string? _dashboardToken;

    public async Task<bool> IsDashboardReachableAsync(CancellationToken cancellationToken = default)
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

    public async Task<string?> SyncCreateTaskAsync(
        WorkTask task,
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_dashboardToken))
        {
            _logger.LogDebug("Kanban create skipped — no dashboard session token");
            return null;
        }

        var board = await FetchBoardTasksInternalAsync(retryAfterRefresh: true, cancellationToken);
        if (board.Outcome == KanbanSyncOutcome.Success)
        {
            var existingId = KanbanTaskMatcher.FindExistingKanbanId(task.Title, workspaceRoot, board.Tasks);
            if (!string.IsNullOrEmpty(existingId))
            {
                _logger.LogInformation(
                    "Kanban create skipped — reusing existing card {KanbanId} for title {Title}",
                    existingId,
                    task.Title);
                return existingId;
            }
        }

        var assignee = task.AssignedAgent == AgentKind.DietCode ? "dietcode" : "hermes";
        var habitatId = task.Id.ToString();
        var description = task.Description ?? "";
        if (!description.Contains("joyzoning:habitat=", StringComparison.OrdinalIgnoreCase))
            description = string.IsNullOrWhiteSpace(description)
                ? $"<!-- joyzoning:habitat={habitatId} -->"
                : $"{description.TrimEnd()}\n\n<!-- joyzoning:habitat={habitatId} -->\n";

        var body = new
        {
            title = task.Title,
            body = description,
            assignee,
            workspace_kind = "dir",
            workspace_path = workspaceRoot,
            idempotency_key = $"jz:habitat:{habitatId}",
        };

        var write = await PostKanbanAsync("tasks", body, cancellationToken);
        if (!write.Success)
        {
            if (write.Outcome == KanbanSyncOutcome.Unauthorized)
                _logger.LogWarning("Kanban create unauthorized — refresh dashboard token via hermes ensure-dashboard");
            return null;
        }

        if (string.IsNullOrEmpty(write.ResponseBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(write.ResponseBody);
            if (doc.RootElement.TryGetProperty("task", out var t) &&
                t.TryGetProperty("id", out var id))
                return id.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse kanban create response");
        }

        return null;
    }

    public Task<KanbanFetchResult> FetchBoardTasksAsync(CancellationToken cancellationToken = default) =>
        FetchBoardTasksInternalAsync(retryAfterRefresh: true, cancellationToken);

    private async Task<KanbanFetchResult> FetchBoardTasksInternalAsync(
        bool retryAfterRefresh,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_dashboardToken))
            return KanbanFetchResult.Skipped("No dashboard session token configured");

        var send = await SendKanbanGetAsync("board", cancellationToken);
        if (send.Outcome == KanbanSyncOutcome.Unauthorized && retryAfterRefresh && await TryRefreshTokenAsync(cancellationToken))
            send = await SendKanbanGetAsync("board", cancellationToken);

        if (send.Outcome != KanbanSyncOutcome.Success || string.IsNullOrEmpty(send.ResponseBody))
        {
            return new KanbanFetchResult(
                Array.Empty<HermesKanbanTaskSnapshot>(),
                send.Outcome,
                send.Detail);
        }

        try
        {
            var tasks = ParseBoardTasks(send.ResponseBody);
            return new KanbanFetchResult(tasks, KanbanSyncOutcome.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kanban board JSON parse failed");
            return new KanbanFetchResult(
                Array.Empty<HermesKanbanTaskSnapshot>(),
                KanbanSyncOutcome.Error,
                ex.Message);
        }
    }

    public async Task SyncUpdateStatusAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_dashboardToken) || string.IsNullOrEmpty(task.HermesKanbanTaskId))
            return;

        var body = new { status = KanbanStatusMapping.ToHermesKanbanStatus(task.Status) };
        var write = await PatchKanbanAsync($"tasks/{task.HermesKanbanTaskId}", body, cancellationToken);
        if (write.Outcome == KanbanSyncOutcome.Unauthorized)
            _logger.LogWarning("Kanban status PATCH unauthorized for task {KanbanId}", task.HermesKanbanTaskId);
    }

    private async Task<KanbanWriteResult> PostKanbanAsync(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        var write = await SendKanbanWithBodyAsync(HttpMethod.Post, path, body, cancellationToken);
        if (write.Outcome == KanbanSyncOutcome.Unauthorized && await TryRefreshTokenAsync(cancellationToken))
            write = await SendKanbanWithBodyAsync(HttpMethod.Post, path, body, cancellationToken);
        return write;
    }

    private async Task<KanbanWriteResult> PatchKanbanAsync(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        var write = await SendKanbanWithBodyAsync(new HttpMethod("PATCH"), path, body, cancellationToken);
        if (write.Outcome == KanbanSyncOutcome.Unauthorized && await TryRefreshTokenAsync(cancellationToken))
            write = await SendKanbanWithBodyAsync(new HttpMethod("PATCH"), path, body, cancellationToken);
        return write;
    }

    private async Task<KanbanWriteResult> SendKanbanGetAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            var url = BuildKanbanUrl(path);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(request);
            var response = await client.SendAsync(request, cancellationToken);
            return await MapResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Kanban GET {Path} unreachable", path);
            return new KanbanWriteResult(false, KanbanSyncOutcome.Unavailable, Detail: ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Kanban GET {Path} timed out", path);
            return new KanbanWriteResult(false, KanbanSyncOutcome.Unavailable, Detail: "Request timed out");
        }
    }

    private async Task<KanbanWriteResult> SendKanbanWithBodyAsync(
        HttpMethod method,
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            var url = BuildKanbanUrl(path);
            var request = new HttpRequestMessage(method, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            AddAuth(request);
            var response = await client.SendAsync(request, cancellationToken);
            return await MapResponseAsync(response, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Kanban {Method} {Path} unreachable", method, path);
            return new KanbanWriteResult(false, KanbanSyncOutcome.Unavailable, Detail: ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Kanban {Method} {Path} timed out", method, path);
            return new KanbanWriteResult(false, KanbanSyncOutcome.Unavailable, Detail: "Request timed out");
        }
    }

    private async Task<KanbanWriteResult> MapResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var code = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
            return new KanbanWriteResult(true, KanbanSyncOutcome.Success, body);

        if (code is 401 or 403)
        {
            _logger.LogWarning("Kanban API auth failure ({Status})", code);
            return new KanbanWriteResult(false, KanbanSyncOutcome.Unauthorized, body, $"HTTP {code}");
        }

        _logger.LogWarning("Kanban API error {Status}: {Body}", code, Truncate(body, 200));
        return new KanbanWriteResult(false, KanbanSyncOutcome.Error, body, $"HTTP {code}");
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenRefresher is null)
        {
            _logger.LogDebug("Kanban auth failure — no IDashboardTokenRefresher registered");
            return false;
        }

        _logger.LogInformation("Kanban API unauthorized — attempting dashboard token refresh");
        return await _tokenRefresher.TryRefreshAsync(cancellationToken);
    }

    private string BuildKanbanUrl(string path) =>
        $"{_settings.GetSnapshot().DashboardBaseUrl}/api/plugins/kanban/{path}";

    private static IReadOnlyList<HermesKanbanTaskSnapshot> ParseBoardTasks(string json)
    {
        var results = new List<HermesKanbanTaskSnapshot>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("columns", out var columns)
            || columns.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var col in columns.EnumerateArray())
        {
            if (!col.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var t in tasks.EnumerateArray())
            {
                var id = t.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(id)) continue;

                var status = t.TryGetProperty("status", out var st) ? st.GetString() ?? "todo" : "todo";
                if (status.Equals("archived", StringComparison.OrdinalIgnoreCase))
                    continue;

                var title = t.TryGetProperty("title", out var ti) ? ti.GetString() ?? id : id;
                var body = t.TryGetProperty("body", out var bo) ? bo.GetString() ?? "" : "";
                var assignee = t.TryGetProperty("assignee", out var asg) ? asg.GetString() : null;
                var workspace = t.TryGetProperty("workspace_path", out var wp) ? wp.GetString() : null;

                results.Add(new HermesKanbanTaskSnapshot(id, title, body, status, assignee, workspace));
            }
        }

        return results;
    }

    private void AddAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_dashboardToken))
            request.Headers.TryAddWithoutValidation("X-Hermes-Session-Token", _dashboardToken);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
