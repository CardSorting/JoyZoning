using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;
using JoyZoning.App.Services.Onboarding;

namespace JoyZoning.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _projectName = "No project";

    [ObservableProperty]
    private string _hermesStatus = "Unknown";

    [ObservableProperty]
    private string _dashboardStatus = "Unknown";

    [ObservableProperty]
    private string _dietCodeStatus = "Idle";

    [ObservableProperty]
    private string _statusLine = "Control plane: checking…";

    [ObservableProperty]
    private Guid? _activeSessionId;

    [ObservableProperty]
    private string? _workspaceRoot;

    [ObservableProperty]
    private bool _showSetupBanner;

    [ObservableProperty]
    private bool _showWorkspaceBanner;

    [ObservableProperty]
    private string _setupBannerMessage =
        "Connect Hermes to enable kanban sync, manager chat, and the embedded TUI.";

    [ObservableProperty]
    private string _setupProgressText = "";

    [ObservableProperty]
    private int _setupProgressPercent;

    [ObservableProperty]
    private bool _showSetupSidebarCard;

    [ObservableProperty]
    private bool _hermesFullyReady;

    [ObservableProperty]
    private int _pendingInterruptedCount;

    [ObservableProperty]
    private string _onboardingToast = "";

    [ObservableProperty]
    private bool _highlightGettingStartedNav;

    [ObservableProperty]
    private bool _showGettingStartedNavBadge;

    [ObservableProperty]
    private bool _isAutoSettingUp;

    [ObservableProperty]
    private string _autoSetupLine = "";

    [ObservableProperty]
    private int _autoSetupPercent;

    public bool PendingWorkspacePicker { get; set; }

    public OnboardingHubViewModel OnboardingHub { get; }
    public SurfaceTipViewModel SurfaceTip { get; }
    public ManagerChatViewModel ManagerChat { get; }
    public KanbanViewModel Kanban { get; }
    public ExecutionViewportViewModel Execution { get; }
    public WorkspaceViewModel Workspace { get; }
    public TimelineViewModel Timeline { get; }
    public ApprovalsViewModel Approvals { get; }
    public HermesConnectionViewModel HermesConnection { get; }

    public MainWindowViewModel()
    {
        OnboardingCoordinator.RecordFirstLaunch();
        OnboardingHub = new OnboardingHubViewModel(this);
        SurfaceTip = new SurfaceTipViewModel();

        HermesConnection = new HermesConnectionViewModel();
        HermesConnection.SetFixHandler(ApplyOnboardingFixAsync);
        HermesConnection.ConnectionChanged += () =>
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await RefreshStatusAsync();
                UpdateOnboardingUi();
            });

        ManagerChat = new ManagerChatViewModel(this);
        Kanban = new KanbanViewModel(this);
        Execution = new ExecutionViewportViewModel(HermesConnection);
        Workspace = new WorkspaceViewModel();
        Timeline = new TimelineViewModel();
        Approvals = new ApprovalsViewModel();

        WireHubEvents();
        _ = InitializeAsync();
    }

    private void WireHubEvents()
    {
        var hub = AppServices.Hub;
        hub.ManagerChatDeltaReceived += delta =>
        {
            if (ActiveSessionId == delta.SessionId)
                ManagerChat.AppendDelta(delta.Delta);
        };
        hub.ManagerChatCompleteReceived += sid =>
        {
            if (ActiveSessionId == sid)
                ManagerChat.CompleteStreaming();
        };
        hub.JoyEventReceived += evt =>
        {
            Timeline.AddEvent(evt);
            Execution.HandleJoyEvent(evt);
            if (ShouldRefreshWorkspaceForEvent(evt))
            {
                var taskId = Kanban.SelectedCard?.Id;
                if (taskId is not null && taskId == evt.CorrelationId)
                    _ = RefreshWorkspaceForTaskAsync(taskId);
            }
        };
        hub.TaskChangedReceived += _ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await Kanban.RefreshAsync();
                if (Kanban.SelectedCard is { } card)
                    await RefreshWorkspaceForTaskAsync(card.Id);
            });
        };
        hub.KanbanSyncedReceived += msg =>
        {
            if (ActiveSessionId is null || ActiveSessionId == msg.SessionId)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => await Kanban.RefreshAsync());
            }
        };
        hub.ApprovalRequestedReceived += _ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () => await Approvals.RefreshAsync());
        };
        hub.ExecutionUpdatedReceived += msg =>
        {
            Execution.UpdateFromRun(msg);
            if (msg.WorkTaskId is { } taskId && Kanban.SelectedCard?.Id == taskId)
                _ = RefreshWorkspaceForTaskAsync(taskId);
        };
        hub.TerminalOutputReceived += msg =>
        {
            if (ActiveSessionId == msg.CorrelationId || Kanban.SelectedCard?.Id == msg.CorrelationId)
                Execution.AppendTerminal(msg.Text);
        };
        hub.WorktreeRefreshedReceived += msg =>
        {
            if (Kanban.SelectedCard?.Id == msg.TaskId)
                _ = RefreshWorkspaceForTaskAsync(msg.TaskId);
        };
    }

    private async Task InitializeAsync()
    {
        var cpStarted = await ControlPlaneHost.EnsureRunningAsync();
        StatusLine = cpStarted
            ? "Control plane: starting…"
            : "Control plane offline — start JoyZoning.ControlPlane manually";

        await RefreshStatusAsync();
        UpdateOnboardingUi();

        try
        {
            await AppServices.Hub.ConnectAsync();
            StatusLine = "Control plane: connected · SignalR live";
        }
        catch
        {
            StatusLine = "Control plane: connected · SignalR offline";
        }

        var prefs = OnboardingPreferences.Load();
        if (!prefs.AutoSetupOnLaunch
            && prefs.AutoConnectHermesOnStartup
            && !HermesFullyReady)
            await ConnectHermesAsync();

        var sessions = await AppServices.ControlPlane.ListSessionsAsync();
        var latest = sessions.FirstOrDefault();
        if (latest is not null)
            await ActivateSessionAsync(latest.Id, latest.Name, latest.WorkspaceRoot);

        await RefreshStatusAsync();
        UpdateOnboardingUi();
    }

    public void UpdateOnboardingUi() => _ = UpdateOnboardingUiAsync();

    private async Task UpdateOnboardingUiAsync()
    {
        var prefs = OnboardingPreferences.Load();
        var hasSession = ActiveSessionId.HasValue;

        await OnboardingHub.RefreshAsync();

        SetupProgressPercent = OnboardingHub.ProgressPercent;
        SetupProgressText = $"Setup {OnboardingHub.ProgressPercent}% — {OnboardingHub.Subheadline}";
        ShowGettingStartedNavBadge = !OnboardingHub.IsComplete;
        HighlightGettingStartedNav = !OnboardingHub.IsComplete;

        ShowWorkspaceBanner = HermesFullyReady && !hasSession;
        Kanban.UpdateHermesBanner(HermesFullyReady, ShowSetupBanner);

        ShowSetupSidebarCard = false;

        if (OnboardingHub.IsComplete && hasSession)
        {
            ShowSetupBanner = false;
            SetupProgressText = "";
            return;
        }

        if (prefs.SetupBannerDismissed && !OnboardingHub.IsComplete)
        {
            ShowSetupBanner = false;
            return;
        }

        ShowSetupBanner = OnboardingHub.ShowTopBanner;
        SetupBannerMessage = OnboardingHub.BannerMessage;
    }

    public void SetOnboardingToast(string message, bool isError = false) =>
        OnboardingToast = (isError ? "⚠ " : "✓ ") + message;

    public OnboardingReportContext CreateOnboardingReportContext() =>
        OnboardingCoordinator.BuildReportContext(
            ActiveSessionId.HasValue,
            HermesFullyReady,
            HermesStatus,
            DashboardStatus,
            ProjectName);

    public void OnSurfaceActivated(string surfaceId) => SurfaceTip.ShowForSurface(surfaceId);

    public bool ShouldLandOnGettingStarted()
    {
        var prefs = OnboardingPreferences.Load();
        if (IsAutoSettingUp)
            return true;
        return prefs.PreferGettingStartedOnLaunch && ShowGettingStartedNavBadge;
    }

    public bool IsOperatorReady() =>
        HermesFullyReady && ActiveSessionId.HasValue;

    /// <summary>Zero-config setup — no wizard, no manual Connection window required.</summary>
    public async Task RunAutomaticOnboardingIfNeededAsync(bool force = false)
    {
        var prefs = OnboardingPreferences.Load();
        if (!force && !prefs.AutoSetupOnLaunch)
            return;

        var assessment = await OnboardingCoordinator.AssessAsync(
            ActiveSessionId.HasValue,
            HermesFullyReady);

        if (!force && assessment.IsComplete && ActiveSessionId.HasValue)
        {
            prefs.WizardCompleted = true;
            prefs.AutoSetupCompletedAt ??= DateTimeOffset.UtcNow;
            prefs.Save();
            return;
        }

        if (!force && assessment.IsComplete && !ActiveSessionId.HasValue)
        {
            await TryAutoOpenWorkspaceAsync();
            await RefreshStatusAsync();
            await UpdateOnboardingUiAsync();
            return;
        }

        if (!force && !assessment.Snapshot.ControlPlaneOk)
        {
            PendingWorkspacePicker = false;
            await UpdateOnboardingUiAsync();
            return;
        }

        IsAutoSettingUp = true;
        AutoSetupLine = "Preparing JoyZoning…";
        AutoSetupPercent = 0;

        try
        {
            var result = await OnboardingAutoSetup.RunAsync(
                HermesConnection,
                line =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => AutoSetupLine = line);
                },
                pct =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => AutoSetupPercent = pct);
                });

            await RefreshStatusAsync();

            if (result.CoreComplete)
            {
                prefs.WizardCompleted = true;
                prefs.WizardDismissed = false;
                prefs.AutoSetupCompletedAt = DateTimeOffset.UtcNow;
                prefs.SetupBannerDismissed = false;
                prefs.PreferGettingStartedOnLaunch = false;
                prefs.Save();

                SetOnboardingToast(result.Message);

                if (result.WorkspacePath is not null)
                    await OpenWorkspaceCommand.ExecuteAsync(result.WorkspacePath);
                else if (result.NeedsWorkspacePicker)
                    PendingWorkspacePicker = true;
            }
            else
            {
                SetOnboardingToast(result.Message, isError: true);
            }

            await OnboardingHub.RefreshAsync();
        }
        finally
        {
            IsAutoSettingUp = false;
            await UpdateOnboardingUiAsync();
        }
    }

    private async Task TryAutoOpenWorkspaceAsync()
    {
        var path = OnboardingAutoSetup.ResolveWorkspacePath();
        if (path is not null)
            await OpenWorkspaceCommand.ExecuteAsync(path);
        else
            PendingWorkspacePicker = true;
    }

    [RelayCommand]
    public async Task UseSampleWorkspaceAsync() =>
        await OpenWorkspaceCommand.ExecuteAsync(OnboardingAutoSetup.EnsureSampleWorkspace());

    [RelayCommand]
    private void DismissSetupBanner()
    {
        var prefs = OnboardingPreferences.Load();
        prefs.SetupBannerDismissed = true;
        prefs.Save();
        ShowSetupBanner = false;
    }

    [RelayCommand]
    private void OpenWorkspaceFromBanner() => RequestWorkspacePicker();

    public async Task ApplyOnboardingFixAsync(string itemId)
    {
        switch (itemId)
        {
            case OnboardingStepIds.ControlPlane:
                await ControlPlaneHost.EnsureRunningAsync();
                break;
            case OnboardingStepIds.CliPaths:
                if (OnboardingPreferences.Load().AutoInstallHermesStack
                    && OnboardingPathDiscovery.BestGuess() is null)
                {
                    var install = await HermesStackInstaller.EnsureInstalledAsync(
                        msg => SetOnboardingToast(msg),
                        _ => { },
                        CancellationToken.None);
                    if (install.Success && install.DietHermesRoot is not null)
                        HermesConnection.InstallRoot = install.DietHermesRoot;
                }
                else
                    HermesConnection.AutoDetectInstallRootCommand.Execute(null);

                await HermesConnection.SavePathsCommand.ExecuteAsync(null);
                break;
            case OnboardingStepIds.Gateway:
                await HermesConnection.EnsureGatewayCommand.ExecuteAsync(null);
                break;
            case OnboardingStepIds.Dashboard:
                await HermesConnection.EnsureDashboardCommand.ExecuteAsync(null);
                break;
            case OnboardingStepIds.Workspace:
                RequestWorkspacePicker();
                break;
        }

        await RefreshStatusAsync();
        await UpdateOnboardingUiAsync();
    }

    [RelayCommand]
    private async Task OpenRecentWorkspaceAsync()
    {
        var path = OnboardingPreferences.Load().LastWorkspacePath;
        if (!string.IsNullOrWhiteSpace(path))
            await OpenWorkspaceCommand.ExecuteAsync(path);
    }

    public async Task OpenHermesTuiAsync()
    {
        Execution.SelectedTabIndex = 1;
        await Execution.ConnectDashboardAndTuiCommand.ExecuteAsync(null);
    }

    public Task<bool> ShouldShowSetupWizardAsync()
    {
        // Manual wizard is opt-in from the menu — first run uses automatic setup instead.
        return Task.FromResult(false);
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        var cpOk = await AppServices.ControlPlane.IsHealthyAsync();
        if (!cpOk)
        {
            StatusLine = "Control plane offline — run JoyZoning.ControlPlane";
            HermesStatus = "Offline";
            DashboardStatus = "Offline";
            HermesFullyReady = false;
            return;
        }

        var hermes = await AppServices.ControlPlane.GetHermesHealthAsync();
        HermesStatus = hermes?.State ?? "Unknown";
        if (hermes?.State is not "Healthy")
            HermesStatus = $"{hermes?.State} — use Connection";

        var dash = await AppServices.ControlPlane.GetDashboardStatusAsync();
        DashboardStatus = dash?.Ready == true
            ? "Connected"
            : dash?.Reachable == true
                ? "Needs connect"
                : "Off";

        HermesFullyReady = hermes?.State == "Healthy" && dash?.Ready == true;
        await HermesConnection.RefreshStatusAsync();
        await HermesConnection.RefreshChecklistAsync(ActiveSessionId.HasValue);
    }

    [RelayCommand]
    public async Task ConnectHermesAsync()
    {
        await HermesConnection.ConnectAllCommand.ExecuteAsync(null);
        await RefreshStatusAsync();
        UpdateOnboardingUi();
    }

    [RelayCommand]
    private async Task EnsureHermesAsync()
    {
        var result = await AppServices.ControlPlane.EnsureHermesAsync();
        HermesStatus = result?.State ?? "Unavailable";
        if (result?.Message is { Length: > 0 } msg && result.State != "Healthy")
            HermesStatus += $" ({msg})";
        await RefreshStatusAsync();
        UpdateOnboardingUi();
    }

    public Task PromptOpenWorkspaceAsync()
    {
        RequestWorkspacePicker();
        return Task.CompletedTask;
    }

    public void RequestWorkspacePicker() => WorkspacePickerRequested?.Invoke();

    public event Action? WorkspacePickerRequested;

    [RelayCommand]
    public async Task OpenWorkspaceAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        var name = new DirectoryInfo(path).Name;
        var session = await AppServices.ControlPlane.CreateSessionAsync(name, path);
        if (session is null) return;

        var prefs = OnboardingPreferences.Load();
        prefs.LastWorkspacePath = path;
        prefs.Save();

        await ActivateSessionAsync(session.Id, session.Name, session.WorkspaceRoot);
        await UpdateOnboardingUiAsync();
    }

    public void MarkOptionalOnboardingStep(string stepId) =>
        OnboardingPreferences.Load().MarkOptionalStep(stepId);

    internal async Task ActivateSessionAsync(Guid sessionId, string name, string workspaceRoot)
    {
        ActiveSessionId = sessionId;
        WorkspaceRoot = workspaceRoot;
        ProjectName = name;

        ManagerChat.SessionId = sessionId;
        Kanban.SessionId = sessionId;
        Workspace.SessionId = sessionId;
        Workspace.WorkspaceRoot = workspaceRoot;
        Workspace.ClearTaskInspection();

        await AppServices.Hub.SubscribeSessionAsync(sessionId);
        await Kanban.RefreshAsync();
        await Approvals.RefreshAsync();
        _ = Workspace.RefreshAsync();

        var events = await AppServices.ControlPlane.ListEventsAsync(sessionId);
        Timeline.LoadEvents(events);
    }

    public async Task CheckInterruptedExecutionsAsync()
    {
        var list = await AppServices.ControlPlane.ListInterruptedExecutionsAsync();
        PendingInterruptedCount = list.Count;
    }

    public Task RefreshWorkspaceForTaskAsync(Guid? taskId) =>
        Workspace.RefreshForTaskAsync(taskId);

    private static bool ShouldRefreshWorkspaceForEvent(JoyEventMessage evt)
    {
        if (evt.Type.Contains("execution.lease", StringComparison.OrdinalIgnoreCase))
            return true;
        if (evt.Type.Contains("dietcode.execution", StringComparison.OrdinalIgnoreCase))
            return true;
        if (evt.Type.Contains("verification.report", StringComparison.OrdinalIgnoreCase))
            return true;
        return evt.Type.Contains("git.status", StringComparison.OrdinalIgnoreCase);
    }
}
