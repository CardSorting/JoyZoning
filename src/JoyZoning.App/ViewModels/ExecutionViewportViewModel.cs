using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;
using SvcSystems.UI.Terminal;

namespace JoyZoning.App.ViewModels;

public partial class ExecutionViewportViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly DashboardPtyClient _pty = new();
    private readonly HermesConnectionViewModel _hermesConnection;

    public TerminalControlModel HermesTerminal { get; } = new(new TerminalOptions
    {
        Cols = 100,
        Rows = 28,
        ReflowOnResize = false,
    });

    [ObservableProperty]
    private string _objective = "No active execution";

    [ObservableProperty]
    private string _activePlan = "—";

    [ObservableProperty]
    private string _terminalOutput =
        "DietCode execution viewport — dispatch a task from Kanban to begin.\n";

    [ObservableProperty]
    private string _ptyStatus = "Disconnected";

    [ObservableProperty]
    private bool _ptyConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _showReconnectBanner;

    public bool ShowConnectionCard => !PtyConnected && !IsConnecting && !ShowReconnectBanner;

    public ObservableCollection<ExecutionStepViewModel> Steps { get; } = new();

    public ExecutionViewportViewModel(HermesConnectionViewModel hermesConnection)
    {
        _hermesConnection = hermesConnection;
        _hermesConnection.ConnectionChanged += () =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(ShowConnectionCard));
            });

        _pty.RawOutputReceived += bytes =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => HermesTerminal.Feed(bytes, bytes.Length));

        _pty.StatusChanged += status =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PtyStatus = status;
                if (PtyConnected) return;

                if (status.Contains("401", StringComparison.OrdinalIgnoreCase)
                    || status.Contains("auth", StringComparison.OrdinalIgnoreCase)
                    || status.Contains("disconnect", StringComparison.OrdinalIgnoreCase))
                {
                    ShowReconnectBanner = true;
                    OnPropertyChanged(nameof(ShowConnectionCard));
                }
            });

        HermesTerminal.UserInput += (_, e) =>
        {
            if (_pty.IsConnected)
                _ = _pty.SendRawAsync(e.Data.ToArray());
        };

        HermesTerminal.SizeChanged += (_, _) =>
        {
            if (_pty.IsConnected)
                _ = _pty.SendResizeAsync(HermesTerminal.Terminal.Cols, HermesTerminal.Terminal.Rows);
        };
    }

    partial void OnPtyConnectedChanged(bool value)
    {
        if (value)
            ShowReconnectBanner = false;
        OnPropertyChanged(nameof(ShowConnectionCard));
    }

    partial void OnIsConnectingChanged(bool value) => OnPropertyChanged(nameof(ShowConnectionCard));

    partial void OnShowReconnectBannerChanged(bool value) => OnPropertyChanged(nameof(ShowConnectionCard));

    [RelayCommand]
    private async Task ReconnectTuiAsync()
    {
        IsConnecting = true;
        ShowReconnectBanner = false;
        try
        {
            PtyStatus = "Refreshing dashboard token…";
            await _hermesConnection.RefreshTokenCommand.ExecuteAsync(null);
            await ConnectPtyCoreAsync();
            if (!PtyConnected)
                ShowReconnectBanner = true;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task ConnectDashboardAndTuiAsync()
    {
        SelectedTabIndex = 1;
        IsConnecting = true;
        try
        {
            PtyStatus = "Connecting dashboard…";
            var ready = await _hermesConnection.EnsureDashboardAsync();
            if (!ready)
            {
                PtyStatus = _hermesConnection.StatusMessage;
                return;
            }

            await ConnectPtyCoreAsync();
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private async Task ConnectPtyAsync()
    {
        SelectedTabIndex = 1;
        IsConnecting = true;
        try
        {
            var settings = await AppServices.ControlPlane.GetSettingsAsync();
            if (settings is null)
            {
                PtyStatus = "Control plane unavailable.";
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.DashboardSessionToken))
            {
                PtyStatus = "Connecting dashboard…";
                var ready = await _hermesConnection.EnsureDashboardAsync();
                if (!ready)
                {
                    PtyStatus = _hermesConnection.StatusMessage;
                    return;
                }

                settings = await AppServices.ControlPlane.GetSettingsAsync();
            }

            if (settings is null || string.IsNullOrWhiteSpace(settings.DashboardSessionToken))
            {
                PtyStatus = "Dashboard token missing — use Connect dashboard & TUI.";
                return;
            }

            await ConnectPtyCoreAsync(settings.DashboardBaseUrl, settings.DashboardSessionToken);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task ConnectPtyCoreAsync(string? baseUrl = null, string? token = null)
    {
        var settings = await AppServices.ControlPlane.GetSettingsAsync();
        baseUrl ??= settings?.DashboardBaseUrl;
        token ??= settings?.DashboardSessionToken;

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
        {
            PtyStatus = "Dashboard not configured.";
            return;
        }

        try
        {
            HermesTerminal.Feed("\r\n\x1b[33mConnecting to Hermes TUI…\x1b[0m\r\n");
            await _pty.ConnectAsync(baseUrl, token);
            PtyConnected = true;
            await _pty.SendResizeAsync(HermesTerminal.Terminal.Cols, HermesTerminal.Terminal.Rows);
        }
        catch (Exception ex)
        {
            PtyStatus = ex.Message;
            PtyConnected = false;
            if (ex.Message.Contains("401", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("auth", StringComparison.OrdinalIgnoreCase))
                ShowReconnectBanner = true;
        }
    }

    [RelayCommand]
    private async Task DisconnectPtyAsync()
    {
        await _pty.DisconnectAsync();
        PtyConnected = false;
        PtyStatus = "Disconnected";
    }

    public async ValueTask DisposeAsync() => await _pty.DisposeAsync();

    public void UpdateFromRun(ExecutionUpdatedMessage msg)
    {
        Objective = msg.Objective;
        ActivePlan = $"Phase: {msg.Phase}";
        AppendTerminal($"[{DateTime.Now:HH:mm:ss}] Execution → {msg.Phase}\n");
    }

    public void HandleJoyEvent(JoyEventMessage evt)
    {
        if (!evt.Type.Contains("tool", StringComparison.OrdinalIgnoreCase) &&
            !evt.Type.Contains("terminal", StringComparison.OrdinalIgnoreCase))
            return;

        if (evt.Type.Contains("tool.started", StringComparison.OrdinalIgnoreCase))
        {
            var name = ExtractToolName(evt.PayloadJson);
            Steps.Add(new ExecutionStepViewModel(Steps.Count + 1, name, "running"));
            AppendTerminal($"▶ {name}\n");
        }
        else if (evt.Type.Contains("tool.completed", StringComparison.OrdinalIgnoreCase))
        {
            AppendTerminal($"✓ tool completed\n");
        }
        else if (evt.Type.Contains("terminal.output", StringComparison.OrdinalIgnoreCase))
        {
            AppendTerminal(ExtractTerminalPreview(evt.PayloadJson));
        }
        else if (evt.Type.Contains("git.status.changed", StringComparison.OrdinalIgnoreCase))
        {
            AppendTerminal($"◇ git status updated ({ExtractFileCount(evt.PayloadJson)} files)\n");
        }
    }

    public void AppendTerminal(string text)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => TerminalOutput += text);
    }

    private static string ExtractToolName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("name", out var n)) return n.GetString() ?? "tool";
        }
        catch (JsonException)
        {
            // ignore malformed payloads
        }

        return "tool";
    }

    private static string ExtractTerminalPreview(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("preview", out var preview))
                return preview.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("text", out var t))
                return t.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("output", out var o))
                return o.GetString() ?? "";
        }
        catch (JsonException)
        {
            // malformed payload from older events
        }

        return "";
    }

    private static int ExtractFileCount(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("fileCount", out var count) && count.TryGetInt32(out var n))
                return n;
        }
        catch (JsonException)
        {
            // ignore
        }

        return 0;
    }
}

public record ExecutionStepViewModel(int Ordinal, string Label, string Status);
