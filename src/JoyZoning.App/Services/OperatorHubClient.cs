using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace JoyZoning.App.Services;

public class OperatorHubClient : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<JoyEventMessage>? JoyEventReceived;
    public event Action<ManagerChatDeltaMessage>? ManagerChatDeltaReceived;
    public event Action<Guid>? ManagerChatCompleteReceived;
    public event Action<TaskChangedMessage>? TaskChangedReceived;
    public event Action<ApprovalRequestedMessage>? ApprovalRequestedReceived;
    public event Action<ExecutionUpdatedMessage>? ExecutionUpdatedReceived;
    public event Action<TerminalOutputMessage>? TerminalOutputReceived;
    public event Action<KanbanSyncedMessage>? KanbanSyncedReceived;
    public event Action<WorktreeRefreshedMessage>? WorktreeRefreshedReceived;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { State: HubConnectionState.Connected })
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl("http://127.0.0.1:9470/hubs/operator")
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JsonElement>("OnJoyEvent", payload =>
        {
            var msg = ParseJoyEvent(payload);
            if (msg is not null)
                JoyEventReceived?.Invoke(msg);
        });

        _connection.On<JsonElement>("OnManagerChatDelta", payload =>
        {
            if (TryGetGuid(payload, "sessionId", out var sid) &&
                payload.TryGetProperty("delta", out var delta))
            {
                ManagerChatDeltaReceived?.Invoke(new ManagerChatDeltaMessage(sid, delta.GetString() ?? ""));
            }
        });

        _connection.On<JsonElement>("OnManagerChatComplete", payload =>
        {
            if (TryGetGuid(payload, "sessionId", out var sid))
                ManagerChatCompleteReceived?.Invoke(sid);
        });

        _connection.On<JsonElement>("OnTaskChanged", payload =>
        {
            var msg = ParseTaskChanged(payload);
            if (msg is not null)
                TaskChangedReceived?.Invoke(msg);
        });

        _connection.On<JsonElement>("OnApprovalRequested", payload =>
        {
            var msg = ParseApproval(payload);
            if (msg is not null)
                ApprovalRequestedReceived?.Invoke(msg);
        });

        _connection.On<JsonElement>("OnExecutionUpdated", payload =>
        {
            var msg = ParseExecution(payload);
            if (msg is not null)
                ExecutionUpdatedReceived?.Invoke(msg);
        });

        _connection.On<JsonElement>("OnTerminalOutput", payload =>
        {
            if (TryGetGuid(payload, "correlationId", out var cid) &&
                payload.TryGetProperty("text", out var text))
            {
                TerminalOutputReceived?.Invoke(new TerminalOutputMessage(cid, text.GetString() ?? ""));
            }
        });

        _connection.On<JsonElement>("OnKanbanSynced", payload =>
        {
            if (TryGetGuid(payload, "sessionId", out var sid))
            {
                var message = payload.TryGetProperty("message", out var m)
                    ? m.GetString() ?? ""
                    : "Kanban synced";
                KanbanSyncedReceived?.Invoke(new KanbanSyncedMessage(sid, message));
            }
        });

        _connection.On<JsonElement>("OnWorktreeRefreshed", payload =>
        {
            if (TryGetGuid(payload, "taskId", out var taskId))
            {
                var root = GetString(payload, "workspaceRoot") ?? "";
                var count = payload.TryGetProperty("fileCount", out var fc) && fc.TryGetInt32(out var n)
                    ? n
                    : 0;
                WorktreeRefreshedReceived?.Invoke(new WorktreeRefreshedMessage(taskId, root, count));
            }
        });

        await _connection.StartAsync(cancellationToken);
    }

    public async Task SubscribeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (_connection is null) return;
        await _connection.InvokeAsync("SubscribeSession", sessionId.ToString(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }

    private static bool TryGetGuid(JsonElement el, string name, out Guid guid)
    {
        guid = Guid.Empty;
        if (!el.TryGetProperty(name, out var prop))
        {
            // PascalCase from SignalR JSON
            var pascal = char.ToUpper(name[0]) + name[1..];
            if (!el.TryGetProperty(pascal, out prop))
                return false;
        }

        var s = prop.GetString();
        return !string.IsNullOrEmpty(s) && Guid.TryParse(s, out guid);
    }

    private static JoyEventMessage? ParseJoyEvent(JsonElement el)
    {
        if (!el.TryGetProperty("type", out var typeProp) && !el.TryGetProperty("Type", out typeProp))
            return null;

        TryGetGuid(el, "correlationId", out var correlationId);

        return new JoyEventMessage(
            el.TryGetProperty("id", out var id) ? id.GetInt64() : el.GetProperty("Id").GetInt64(),
            correlationId,
            GetString(el, "source") ?? "",
            typeProp.GetString() ?? "",
            GetString(el, "payloadJson") ?? "{}",
            el.TryGetProperty("occurredAt", out var at) && at.TryGetDateTimeOffset(out var when)
                ? when
                : DateTimeOffset.UtcNow);
    }

    private static TaskChangedMessage? ParseTaskChanged(JsonElement el)
    {
        if (!TryGetGuid(el, "id", out var id) && !TryGetGuid(el, "Id", out id))
            return null;

        return new TaskChangedMessage(
            id,
            GetString(el, "title") ?? "",
            GetString(el, "status") ?? "");
    }

    private static ApprovalRequestedMessage? ParseApproval(JsonElement el)
    {
        if (!TryGetGuid(el, "id", out var id))
            return null;

        return new ApprovalRequestedMessage(
            id,
            GetString(el, "command") ?? "",
            GetString(el, "description") ?? "",
            GetString(el, "risk") ?? "Low");
    }

    private static ExecutionUpdatedMessage? ParseExecution(JsonElement el)
    {
        if (!TryGetGuid(el, "id", out var id))
            return null;

        TryGetGuid(el, "workTaskId", out var taskId);

        return new ExecutionUpdatedMessage(
            id,
            taskId == Guid.Empty ? null : taskId,
            GetString(el, "objective") ?? "",
            GetString(el, "phase") ?? "");
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p)) return p.GetString();
        var pascal = char.ToUpper(name[0]) + name[1..];
        return el.TryGetProperty(pascal, out p) ? p.GetString() : null;
    }
}

public record JoyEventMessage(
    long Id,
    Guid CorrelationId,
    string Source,
    string Type,
    string PayloadJson,
    DateTimeOffset OccurredAt);
public record ManagerChatDeltaMessage(Guid SessionId, string Delta);
public record TaskChangedMessage(Guid Id, string Title, string Status);
public record ApprovalRequestedMessage(Guid Id, string Command, string Description, string Risk);
public record ExecutionUpdatedMessage(Guid Id, Guid? WorkTaskId, string Objective, string Phase);
public record TerminalOutputMessage(Guid CorrelationId, string Text);
public record KanbanSyncedMessage(Guid SessionId, string Message);
public record WorktreeRefreshedMessage(Guid TaskId, string WorkspaceRoot, int FileCount);
