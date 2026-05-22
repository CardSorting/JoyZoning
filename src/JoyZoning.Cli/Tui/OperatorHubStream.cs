using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace JoyZoning.Cli.Tui;

/// <summary>Live control-plane event stream (Hermes gateway event pump analogue).</summary>
public sealed class OperatorHubStream : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;
    private readonly object _writeLock = new();

    public event Action<StreamLine>? LineReceived;

    public OperatorHubStream(string controlPlaneBaseUrl)
    {
        var baseUri = controlPlaneBaseUrl.TrimEnd('/');
        _hubUrl = $"{baseUri}/hubs/operator";
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { State: HubConnectionState.Connected })
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JsonElement>("OnJoyEvent", payload =>
        {
            var line = FormatJoyEvent(payload);
            if (line is not null)
                Emit(line);
        });

        _connection.On<JsonElement>("OnManagerChatDelta", payload =>
        {
            if (!payload.TryGetProperty("delta", out var delta) &&
                !payload.TryGetProperty("Delta", out delta))
                return;
            Emit(new StreamLine("manager", delta.GetString() ?? "", IsDelta: true));
        });

        _connection.On<JsonElement>("OnManagerChatComplete", _ =>
            Emit(new StreamLine("manager", "\n")));

        _connection.On<JsonElement>("OnApprovalRequested", payload =>
        {
            var cmd = GetString(payload, "command") ?? "approval";
            var risk = GetString(payload, "risk") ?? "?";
            Emit(new StreamLine("approval", $"⚠ approval requested: {cmd} (risk {risk})"));
        });

        _connection.On<JsonElement>("OnExecutionUpdated", payload =>
        {
            var phase = GetString(payload, "phase") ?? "?";
            var objective = GetString(payload, "objective") ?? "";
            Emit(new StreamLine("execution", $"▸ {phase}: {objective}"));
        });

        await _connection.StartAsync(cancellationToken);
    }

    public async Task SubscribeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        if (_connection is null)
            return;
        await _connection.InvokeAsync("SubscribeSession", sessionId.ToString(), cancellationToken);
    }

    private void Emit(StreamLine line)
    {
        lock (_writeLock)
            LineReceived?.Invoke(line);
    }

    private static StreamLine? FormatJoyEvent(JsonElement el)
    {
        var type = GetString(el, "type");
        if (type is null)
            return null;

        if (type.StartsWith("hermes.tool.", StringComparison.Ordinal))
        {
            var payload = ParsePayload(el);
            var tool = payload.TryGetProperty("tool", out var t) ? t.GetString() : type;
            var summary = payload.TryGetProperty("summary", out var s) ? s.GetString() : "";
            return new StreamLine("tool", $"┊ {tool} {summary}".Trim());
        }

        if (type == "hermes.message.delta")
        {
            var payload = ParsePayload(el);
            if (payload.TryGetProperty("delta", out var d))
                return new StreamLine("hermes", d.GetString() ?? "", IsDelta: true);
        }

        if (type is "hermes.run.started" or "hermes.run.completed" or
            "dietcode.execution.started" or "dietcode.execution.completed" or
            "task.status_changed")
        {
            return new StreamLine("event", $"• {type}");
        }

        return null;
    }

    private static JsonElement ParsePayload(JsonElement el)
    {
        var raw = GetString(el, "payloadJson") ?? "{}";
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(raw);
        }
        catch
        {
            return default;
        }
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p))
            return p.GetString();
        var pascal = char.ToUpper(name[0]) + name[1..];
        return el.TryGetProperty(pascal, out p) ? p.GetString() : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}

public sealed record StreamLine(string Kind, string Text, bool IsDelta = false);
