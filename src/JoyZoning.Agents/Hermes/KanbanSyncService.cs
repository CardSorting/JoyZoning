using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Mapping;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Agents.Hermes;

/// <summary>
/// Best-effort sync to diet-hermes kanban dashboard plugin when reachable.
/// Requires optional dashboard session token in JoyZoning config.
/// </summary>
public class KanbanSyncService
{
    private readonly HermesRuntimeSettings _settings;
    private readonly ILogger<KanbanSyncService> _logger;
    private readonly HttpClient _http;

    public KanbanSyncService(HermesRuntimeSettings settings, ILogger<KanbanSyncService> logger)
    {
        _settings = settings;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
    }

    public void SetDashboardToken(string? token) => _dashboardToken = token;

    private string? _dashboardToken;

    public async Task<bool> IsDashboardReachableAsync(CancellationToken cancellationToken = default)
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

    public async Task<string?> SyncCreateTaskAsync(WorkTask task, string workspaceRoot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_dashboardToken))
        {
            _logger.LogDebug("Kanban sync skipped — no dashboard session token configured");
            return null;
        }

        var assignee = task.AssignedAgent == AgentKind.DietCode ? "dietcode" : "hermes";
        var body = new
        {
            title = task.Title,
            body = task.Description,
            assignee,
            workspace_kind = "dir",
            workspace_path = workspaceRoot,
        };

        var response = await PostKanbanAsync("tasks", body, cancellationToken);
        if (response is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(response);
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

    public async Task<IReadOnlyList<HermesKanbanTaskSnapshot>> FetchBoardTasksAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_dashboardToken))
            return Array.Empty<HermesKanbanTaskSnapshot>();

        try
        {
            var url = $"{_settings.DashboardBaseUrl}/api/plugins/kanban/board";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(request);
            var r = await _http.SendAsync(request, cancellationToken);
            if (!r.IsSuccessStatusCode)
            {
                _logger.LogDebug("Kanban GET board failed: {Code}", (int)r.StatusCode);
                return Array.Empty<HermesKanbanTaskSnapshot>();
            }

            var json = await r.Content.ReadAsStringAsync(cancellationToken);
            return ParseBoardTasks(json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kanban GET board unavailable");
            return Array.Empty<HermesKanbanTaskSnapshot>();
        }
    }

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

    public async Task SyncUpdateStatusAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_dashboardToken) || string.IsNullOrEmpty(task.HermesKanbanTaskId))
            return;

        var body = new { status = KanbanStatusMapping.ToHermesKanbanStatus(task.Status) };
        await PatchKanbanAsync($"tasks/{task.HermesKanbanTaskId}", body, cancellationToken);
    }

    private async Task<string?> PostKanbanAsync(string path, object body, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{_settings.DashboardBaseUrl}/api/plugins/kanban/{path}";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            AddAuth(request);
            var r = await _http.SendAsync(request, cancellationToken);
            if (!r.IsSuccessStatusCode)
            {
                _logger.LogDebug("Kanban POST {Path} failed: {Code}", path, (int)r.StatusCode);
                return null;
            }
            return await r.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kanban POST {Path} unavailable", path);
            return null;
        }
    }

    private async Task PatchKanbanAsync(string path, object body, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{_settings.DashboardBaseUrl}/api/plugins/kanban/{path}";
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            AddAuth(request);
            var r = await _http.SendAsync(request, cancellationToken);
            if (!r.IsSuccessStatusCode)
                _logger.LogDebug("Kanban PATCH {Path} failed: {Code}", path, (int)r.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kanban PATCH {Path} unavailable", path);
        }
    }

    private void AddAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_dashboardToken))
            request.Headers.TryAddWithoutValidation("X-Hermes-Session-Token", _dashboardToken);
    }
}
