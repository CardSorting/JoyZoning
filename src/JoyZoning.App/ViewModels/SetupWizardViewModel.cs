using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;

namespace JoyZoning.App.ViewModels;

public partial class SetupWizardViewModel : ViewModelBase
{
    private readonly HermesConnectionViewModel _connection;
    private readonly MainWindowViewModel _shell;
    private readonly Func<Task>? _openWorkspace;
    private readonly Func<Task<string?>>? _browseInstallRoot;

    [ObservableProperty]
    private int _currentStep;

    [ObservableProperty]
    private string _statusLine = "";

    [ObservableProperty]
    private string _pathValidationMessage = "";

    [ObservableProperty]
    private bool _pathsValid;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hermesReady;

    [ObservableProperty]
    private bool _autoConnectOnStartup = true;

    [ObservableProperty]
    private int _setupProgress;

    [ObservableProperty]
    private bool _hasWorkspace;

    [ObservableProperty]
    private bool _openTuiOnFinish;

    [ObservableProperty]
    private string _detectedPathsSummary = "";

    [ObservableProperty]
    private bool _hasDetectedPaths;

    [ObservableProperty]
    private bool _hasRecentWorkspace;

    [ObservableProperty]
    private string _recentWorkspaceLabel = "";

    public ObservableCollection<OnboardingChecklistItemViewModel> Checklist { get; } = new();
    public ObservableCollection<string> DetectedInstallRoots { get; } = new();

    public int TotalSteps => 4;

    public string StepTitle => CurrentStep switch
    {
        0 => "Welcome",
        1 => "Hermes paths",
        2 => "Connect Hermes",
        3 => "You're almost ready",
        _ => "",
    };

    public string StepIndicator => $"Step {CurrentStep + 1} of {TotalSteps}";

    public string ProgressDots => string.Concat(Enumerable.Range(0, TotalSteps)
        .Select(i => i == CurrentStep ? " ●" : " ○"));

    public bool CanGoBack => CurrentStep > 0 && !IsBusy;
    public bool CanGoNext => CurrentStep < 2 && !IsBusy && (CurrentStep != 1 || PathsValid);
    public bool ShowConnectStep => CurrentStep == 2;
    public bool ShowWorkspaceStep => CurrentStep == 3;
    public bool ShowFinishWithoutHermes => CurrentStep == 3 && !HermesReady;

    public HermesConnectionViewModel Connection => _connection;

    public SetupWizardViewModel(
        MainWindowViewModel shell,
        HermesConnectionViewModel connection,
        Func<Task>? openWorkspace = null,
        Func<Task<string?>>? browseInstallRoot = null)
    {
        _shell = shell;
        _connection = connection;
        _openWorkspace = openWorkspace;
        _browseInstallRoot = browseInstallRoot;
        var prefs = OnboardingPreferences.Load();
        AutoConnectOnStartup = prefs.AutoConnectHermesOnStartup;
        OpenTuiOnFinish = prefs.OpenTuiAfterWizard;
        RefreshRecentWorkspace(prefs);
        _connection.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HermesConnectionViewModel.InstallRoot))
                ValidatePaths();
        };
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepIndicator));
        OnPropertyChanged(nameof(ProgressDots));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowConnectStep));
        OnPropertyChanged(nameof(ShowWorkspaceStep));
        OnPropertyChanged(nameof(ShowFinishWithoutHermes));
        _ = OnStepEnteredAsync();
    }

    public async Task InitializeAsync()
    {
        await _connection.LoadAsync();
        ValidatePaths();
        RefreshDetectedPaths();
        await RefreshSnapshotAsync();

        if (HermesReady && SetupProgress >= 75)
            StatusLine = "Hermes is already connected — finish by opening a workspace.";
    }

    private void RefreshRecentWorkspace(OnboardingPreferences? prefs = null)
    {
        prefs ??= OnboardingPreferences.Load();
        var path = prefs.LastWorkspacePath;
        HasRecentWorkspace = !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        RecentWorkspaceLabel = HasRecentWorkspace
            ? $"Last workspace: {path}"
            : "";
    }

    public void RefreshDetectedPaths()
    {
        DetectedInstallRoots.Clear();
        foreach (var p in OnboardingPathDiscovery.DiscoverInstallRoots(_connection.InstallRoot))
            DetectedInstallRoots.Add(p);

        HasDetectedPaths = DetectedInstallRoots.Count > 0;
        DetectedPathsSummary = HasDetectedPaths
            ? $"{DetectedInstallRoots.Count} install path(s) detected"
            : "No diet-hermes folder detected — use Browse";
    }

    [RelayCommand]
    private void AutoDetectInstallRoot()
    {
        var best = OnboardingPathDiscovery.BestGuess(_connection.InstallRoot);
        if (best is not null)
        {
            _connection.InstallRoot = best;
            ValidatePaths();
            StatusLine = $"Using {best}";
        }
        else
        {
            StatusLine = "No valid install found in Downloads or env vars — browse manually.";
        }

        RefreshDetectedPaths();
    }

    [RelayCommand]
    private void UseDetectedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _connection.InstallRoot = path;
        ValidatePaths();
        RefreshDetectedPaths();
    }

    [RelayCommand]
    private async Task FixChecklistItemAsync(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        await _shell.ApplyOnboardingFixAsync(itemId);
        await RefreshSnapshotAsync();
    }

    [RelayCommand]
    private async Task OpenRecentWorkspaceAsync()
    {
        var path = OnboardingPreferences.Load().LastWorkspacePath;
        if (!string.IsNullOrWhiteSpace(path))
            await _shell.OpenWorkspaceCommand.ExecuteAsync(path);
        await RefreshSnapshotAsync();
    }

    private async Task OnStepEnteredAsync()
    {
        if (CurrentStep == 1)
            RefreshDetectedPaths();

        await RefreshSnapshotAsync();
        if (CurrentStep == 2 && !HermesReady && !IsBusy)
            StatusLine = "Click Connect Hermes to start the gateway and dashboard.";
    }

    public void ValidatePaths()
    {
        var result = OnboardingEvaluator.ValidateInstallRoot(_connection.InstallRoot);
        PathsValid = result.IsValid;
        PathValidationMessage = result.Message;
    }

    [RelayCommand]
    private async Task BrowseInstallRootAsync()
    {
        if (_browseInstallRoot is null) return;
        var picked = await _browseInstallRoot();
        if (!string.IsNullOrWhiteSpace(picked))
        {
            _connection.InstallRoot = picked;
            ValidatePaths();
        }
    }

    [RelayCommand]
    private async Task NextStep()
    {
        if (CurrentStep == 1)
        {
            if (!PathsValid)
            {
                AutoDetectInstallRootCommand.Execute(null);
                if (!PathsValid)
                {
                    StatusLine = PathValidationMessage;
                    return;
                }
            }

            await _connection.SavePathsCommand.ExecuteAsync(null);
            StatusLine = "Paths saved.";
        }

        if (CurrentStep < 2)
            CurrentStep++;
    }

    [RelayCommand]
    private void BackStep()
    {
        if (CurrentStep > 0)
            CurrentStep--;
    }

    [RelayCommand]
    private async Task ConnectHermesAsync()
    {
        IsBusy = true;
        StatusLine = "Starting gateway and dashboard…";
        try
        {
            await _connection.SavePathsCommand.ExecuteAsync(null);
            HermesReady = await _connection.EnsureDashboardAsync();
            StatusLine = _connection.StatusMessage;
            if (HermesReady && CurrentStep == 2)
            {
                StatusLine += " Advancing to workspace step…";
                CurrentStep = 3;
            }
        }
        finally
        {
            IsBusy = false;
            await RefreshSnapshotAsync();
        }
    }

    [RelayCommand]
    private async Task OpenWorkspaceAsync()
    {
        if (_openWorkspace is not null)
            await _openWorkspace();
        RefreshRecentWorkspace();
        await RefreshSnapshotAsync();
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        var prefs = OnboardingPreferences.Load();
        prefs.WizardCompleted = true;
        prefs.WizardDismissed = false;
        prefs.AutoConnectHermesOnStartup = AutoConnectOnStartup;
        prefs.OpenTuiAfterWizard = OpenTuiOnFinish;
        prefs.SetupBannerDismissed = false;
        prefs.Save();

        await _shell.RefreshStatusCommand.ExecuteAsync(null);
        _shell.UpdateOnboardingUi();

        if (OpenTuiOnFinish && HermesReady)
            await _shell.OpenHermesTuiAsync();

        WizardFinished?.Invoke(true);
    }

    [RelayCommand]
    private void SkipForNow()
    {
        var prefs = OnboardingPreferences.Load();
        prefs.WizardDismissed = true;
        prefs.AutoConnectHermesOnStartup = AutoConnectOnStartup;
        prefs.Save();

        _shell.UpdateOnboardingUi();
        WizardFinished?.Invoke(false);
    }

    public event Action<bool>? WizardFinished;

    private async Task RefreshSnapshotAsync()
    {
        var snap = await OnboardingEvaluator.AssessAsync(_shell.ActiveSessionId.HasValue);
        HermesReady = snap.GatewayOk && snap.DashboardOk;
        SetupProgress = snap.ProgressPercent;
        HasWorkspace = snap.WorkspaceOk;
        _connection.DashboardReady = snap.DashboardOk;

        Checklist.Clear();
        foreach (var item in OnboardingEvaluator.ToChecklist(snap))
            Checklist.Add(new OnboardingChecklistItemViewModel(item.Id, item.Label, item.IsComplete, item.FixHint));
    }
}

public partial class OnboardingChecklistItemViewModel : ViewModelBase
{
    public OnboardingChecklistItemViewModel(string id, string label, bool isComplete, string? fixHint = null)
    {
        Id = id;
        Label = label;
        IsComplete = isComplete;
        FixHint = fixHint;
    }

    public string Id { get; }
    public string Label { get; }
    public bool IsComplete { get; }
    public string? FixHint { get; }
    public string StatusIcon => IsComplete ? "✓" : "○";
    public bool ShowFix => !IsComplete && !string.IsNullOrWhiteSpace(FixHint);
}
