using System.Net.WebSockets;
using System.Text;

namespace JoyZoning.App.Services;

/// <summary>
/// Connects to diet-hermes dashboard <c>/api/pty</c> WebSocket (raw PTY bytes, VT100).
/// </summary>
public sealed class DashboardPtyClient : IAsyncDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public event Action<byte[]>? RawOutputReceived;
    public event Action<string>? StatusChanged;

    public async Task ConnectAsync(
        string dashboardBaseUrl,
        string sessionToken,
        string? channel = null,
        CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        var baseUri = dashboardBaseUrl.TrimEnd('/');
        var wsBase = baseUri.Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase);

        channel ??= $"joyzoning-{Guid.NewGuid():N}";
        var url = $"{wsBase}/api/pty?token={Uri.EscapeDataString(sessionToken)}&channel={Uri.EscapeDataString(channel)}";

        _ws = new ClientWebSocket();
        _readCts = new CancellationTokenSource();

        try
        {
            await _ws.ConnectAsync(new Uri(url), cancellationToken);
            StatusChanged?.Invoke("Hermes TUI connected (VT100).");
            _readTask = Task.Run(() => ReadLoopAsync(_readCts.Token), CancellationToken.None);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"PTY connect failed: {ex.Message}");
            await DisconnectAsync();
            throw;
        }
    }

    public async Task SendRawAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_ws?.State != WebSocketState.Open || data.Length == 0)
            return;

        await _ws.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
    }

    public async Task SendResizeAsync(int cols, int rows, CancellationToken cancellationToken = default)
    {
        var seq = $"\x1b[RESIZE:{cols};{rows}]";
        await SendRawAsync(Encoding.UTF8.GetBytes(seq), cancellationToken);
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        try
        {
            while (_ws?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.Count > 0)
                {
                    var chunk = new byte[result.Count];
                    Buffer.BlockCopy(buffer, 0, chunk, 0, result.Count);
                    RawOutputReceived?.Invoke(chunk);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"PTY disconnected: {ex.Message}");
        }
        finally
        {
            StatusChanged?.Invoke("Hermes TUI disconnected.");
        }
    }

    public async Task DisconnectAsync()
    {
        _readCts?.Cancel();
        if (_readTask is not null)
        {
            try { await _readTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { /* ignore */ }
        }

        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { /* ignore */ }
        }

        _ws?.Dispose();
        _ws = null;
        _readCts?.Dispose();
        _readCts = null;
        _readTask = null;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
