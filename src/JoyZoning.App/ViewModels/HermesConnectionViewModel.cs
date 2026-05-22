using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;

namespace JoyZoning.App.ViewModels;

public partial class HermesConnectionViewModel : ViewModelBase
{
    private Func<string, Task>? _fixHandler;
    [ObservableProperty]
    private string _installRoot = "";

    [ObservableProperty]
    private string _apiBaseUrl = "";

    [ObservableProperty]
    private string _dashboardBaseUrl = "";

    [ObservableProperty]
    private string _profile = "";

    [ObservableProperty]
    private string _manualToken = "";

    [ObservableProperty]
    private string _gatewayStatus = "Unknown";

    [ObservableProperty]
    private string _dashboardStatus = "Unknown";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showAdvanced;

    [ObservableProperty]
    private bool _dashboardReady;

    [ObservableProperty]
    private int _setupProgress;

    [ObservableProperty]
    private string _pathValidationMessage = "";

    [ObservableProperty]
    private bool _pathsValid = true;

    public ObservableCollection<ConnectionStepViewModel> Steps { get; } = new();
    public ObservableCollection<OnboardingChecklistItemViewModel> Checklist { get; } = new();

    public event Action? ConnectionChanged;

    public void SetFixHandler(Func<string, Task> handler) => _fixHandler = handler;

    partial void OnInstallRootChanged(string value) => ValidatePaths();

    public async Task LoadAsync()
    {
        var s = await AppServices.ControlPlane.GetSettingsAsync();
        if (s is null) return;

        InstallRoot = s.InstallRoot;
        ApiBaseUrl = s.ApiBaseUrl;
        DashboardBaseUrl = s.DashboardBaseUrl;
        Profile = s.Profile;
        ManualToken = s.DashboardSessionToken;
        ValidatePaths();
        await RefreshStatusAsync();
    }

    public void ValidatePaths()
    {
        var result = OnboardingEvaluator.ValidateInstallRoot(InstallRoot);
        PathsValid = result.IsValid;
        PathValidationMessage = result.Message;
    }

    public async Task RefreshChecklistAsync(bool hasActiveSession)
    {
        var snap = await OnboardingEvaluator.AssessAsync(hasActiveSession);
        SetupProgress = snap.ProgressPercent;
        PathsValid = snap.PathsValid;
        if (!snap.PathsValid)
            PathValidationMessage = OnboardingEvaluator.ValidateInstallRoot(InstallRoot).Message;

        Checklist.Clear();
        foreach (var item in OnboardingEvaluator.ToChecklist(snap))
            Checklist.Add(new OnboardingChecklistItemViewModel(item.Id, item.Label, item.IsComplete, item.FixHint));
    }

    [RelayCommand]
    private async Task FixChecklistItemAsync(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || _fixHandler is null) return;
        await _fixHandler(itemId);
        await RefreshChecklistAsync(hasActiveSession: true);
    }

    [RelayCommand]
    private void AutoDetectInstallRoot()
    {
        var best = OnboardingPathDiscovery.BestGuess(InstallRoot);
        if (best is not null)
            InstallRoot = best;
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        var gw = await AppServices.ControlPlane.GetHermesHealthAsync();
        GatewayStatus = gw?.State ?? "Offline";

        var dash = await AppServices.ControlPlane.GetDashboardStatusAsync();
        if (dash is null)
        {
            DashboardStatus = "Offline";
            DashboardReady = false;
            StatusMessage = "Control plane unavailable";
            return;
        }

        DashboardReady = dash.Ready;
        DashboardStatus = dash.Ready
            ? "Connected"
            : dash.Reachable
                ? dash.HasToken ? "Token stale" : "Needs connect"
                : "Not running";

        StatusMessage = dash.Message;
        if (!dash.HermesCliFound)
            StatusMessage = "hermes CLI not found — check install root";
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsBusy = true;
        Steps.Clear();
        try
        {
            await SavePathsAsync();
            var snap = await OnboardingEvaluator.AssessAsync(hasActiveSession: true);
            if (!snap.GatewayOk)
            {
                var gw = await AppServices.ControlPlane.EnsureHermesAsync();
                Steps.Add(new ConnectionStepViewModel("API gateway", gw?.State == "Healthy" ? "done" : "failed", gw?.Message));
            }
            else
            {
                Steps.Add(new ConnectionStepViewModel("API gateway", "done", "Already healthy"));
            }

            if (!snap.DashboardOk)
            {
                var ok = await EnsureDashboardAsync();
                Steps.Add(new ConnectionStepViewModel("Dashboard", ok ? "done" : "failed", StatusMessage));
            }
            else
            {
                Steps.Add(new ConnectionStepViewModel("Dashboard", "done", "Connected"));
            }

            await RefreshStatusAsync();
            await RefreshChecklistAsync(hasActiveSession: true);
            ConnectionChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SavePathsAsync()
    {
        var current = await AppServices.ControlPlane.GetSettingsAsync();
        if (current is null) return;

        var settings = new ControlPlaneClient.AppSettings(
            InstallRoot,
            ApiBaseUrl,
            DashboardBaseUrl,
            Profile,
            string.IsNullOrWhiteSpace(ManualToken) ? current.DashboardSessionToken : ManualToken,
            current.DashboardReachable,
            current.AutoSyncEnabled,
            current.AutoSyncIntervalSeconds);

        await AppServices.ControlPlane.SaveSettingsAsync(settings);
        await RefreshStatusAsync();
        ConnectionChanged?.Invoke();
    }

    [RelayCommand]
    private async Task EnsureGatewayAsync()
    {
        IsBusy = true;
        Steps.Clear();
        try
        {
            var result = await AppServices.ControlPlane.EnsureHermesAsync();
            GatewayStatus = result?.State ?? "Failed";
            StatusMessage = result?.Message ?? "Gateway ensure failed";
        }
        finally
        {
            IsBusy = false;
            await RefreshStatusAsync();
            ConnectionChanged?.Invoke();
        }
    }

    [RelayCommand]
    public async Task<bool> EnsureDashboardAsync()
    {
        IsBusy = true;
        Steps.Clear();
        try
        {
            await SavePathsAsync();
            var result = await AppServices.ControlPlane.EnsureDashboardAsync(alsoEnsureGateway: true);
            if (result is null)
            {
                StatusMessage = "Dashboard connect failed — control plane error";
                return false;
            }

            foreach (var step in result.Steps)
                Steps.Add(new ConnectionStepViewModel(step.Label, step.Status, step.Detail));

            StatusMessage = result.Message;
            DashboardReady = result.Ready;

            if (result.TokenAcquired)
            {
                var s = await AppServices.ControlPlane.GetSettingsAsync();
                if (s is not null)
                    ManualToken = s.DashboardSessionToken;
            }

            await RefreshStatusAsync();
            ConnectionChanged?.Invoke();
            return result.Ready;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshTokenAsync()
    {
        IsBusy = true;
        Steps.Clear();
        try
        {
            var result = await AppServices.ControlPlane.RefreshDashboardTokenAsync();
            if (result is null)
            {
                StatusMessage = "Token refresh failed";
                return;
            }

            StatusMessage = result.Message;
            DashboardReady = result.Ready;
            if (result.TokenAcquired)
            {
                var s = await AppServices.ControlPlane.GetSettingsAsync();
                if (s is not null)
                    ManualToken = s.DashboardSessionToken;
            }

            await RefreshStatusAsync();
            ConnectionChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConnectAllAsync()
    {
        await EnsureGatewayAsync();
        await EnsureDashboardAsync();
    }

    public bool IsFullyReady =>
        GatewayStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase) && DashboardReady;
}

public record ConnectionStepViewModel(string Label, string Status, string? Detail)
{
    public string StatusIcon => Status switch
    {
        "done" => "✓",
        "failed" => "✗",
        "running" => "…",
        "skipped" => "○",
        _ => "·",
    };
}
