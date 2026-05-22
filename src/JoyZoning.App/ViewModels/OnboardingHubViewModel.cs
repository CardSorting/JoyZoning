using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;
using JoyZoning.App.Services.Onboarding;

namespace JoyZoning.App.ViewModels;

public partial class OnboardingHubViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private OnboardingAssessment? _lastAssessment;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _phaseLabel = "GETTING STARTED";

    [ObservableProperty]
    private string _headline = "Welcome to JoyZoning";

    [ObservableProperty]
    private string _subheadline = "";

    [ObservableProperty]
    private string _primaryActionLabel = "Continue setup";

    [ObservableProperty]
    private string? _primaryActionStepId;

    [ObservableProperty]
    private string _timeEstimateText = "";

    [ObservableProperty]
    private string _healthGradeLabel = "Blocked";

    [ObservableProperty]
    private string _healthGradeColor = "#f85149";

    [ObservableProperty]
    private string _stepsSummaryText = "0 / 5 complete";

    [ObservableProperty]
    private string? _highlightedStepId;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _showCelebration;

    [ObservableProperty]
    private bool _showDiagnostics;

    [ObservableProperty]
    private bool _showTopBanner = true;

    [ObservableProperty]
    private string _bannerMessage = "";

    [ObservableProperty]
    private bool _isRunningSmartSetup;

    [ObservableProperty]
    private string? _coreCompletedAgoText;

    public ObservableCollection<OnboardingStepRowViewModel> Steps { get; } = new();
    public ObservableCollection<OnboardingDiagnosticViewModel> Diagnostics { get; } = new();
    public ObservableCollection<OnboardingWhatsNextCardViewModel> WhatsNext { get; } = new();
    public ObservableCollection<OnboardingStepRowViewModel> OptionalSteps { get; } = new();
    public ObservableCollection<OnboardingQuickActionViewModel> QuickActions { get; } = new();
    public ObservableCollection<OnboardingResourceLinkViewModel> Resources { get; } = new();
    public ObservableCollection<OnboardingPlaybookViewModel> Playbooks { get; } = new();

    public event Func<Task>? OpenWizardRequested;
    public event Func<Task>? OpenConnectionRequested;
    public event Action<string>? NavigateSurfaceRequested;
    public event Func<string, Task>? CopyTextRequested;

    public OnboardingHubViewModel(MainWindowViewModel shell) => _shell = shell;

    public async Task RefreshAsync()
    {
        OnboardingCoordinator.RecordHubVisit();

        var assessment = await OnboardingCoordinator.AssessAsync(
            _shell.ActiveSessionId.HasValue,
            _shell.HermesFullyReady);

        _lastAssessment = assessment;
        OnboardingCoordinator.SyncCoreStepCompletions(assessment.Snapshot, _shell.ActiveSessionId.HasValue);

        var prefs = OnboardingPreferences.Load();
        var grade = OnboardingHealthReport.Grade(assessment);

        ProgressPercent = assessment.Snapshot.ProgressPercent;
        PhaseLabel = OnboardingCatalog.PhaseLabel(assessment.Phase);
        Headline = assessment.Headline;
        Subheadline = assessment.Subheadline;
        PrimaryActionLabel = assessment.PrimaryActionLabel;
        PrimaryActionStepId = assessment.PrimaryActionStepId;
        HighlightedStepId = assessment.PrimaryActionStepId;
        TimeEstimateText = assessment.EstimatedMinutesRemaining > 0
            ? $"~{assessment.EstimatedMinutesRemaining} min to finish core setup"
            : "Core setup complete";
        HealthGradeLabel = OnboardingHealthReport.GradeLabel(grade);
        HealthGradeColor = OnboardingHealthReport.GradeColor(grade);
        StepsSummaryText = $"{assessment.Steps.Count(s => s.IsComplete)} / {assessment.Steps.Count} core steps";
        IsComplete = assessment.IsComplete;
        ShowCelebration = OnboardingCoordinator.ShouldShowCelebration(assessment.IsComplete);
        ShowDiagnostics = assessment.Diagnostics.Any(d => d.Severity != OnboardingDiagnosticSeverity.Info);
        ShowTopBanner = !assessment.IsComplete && !prefs.SetupBannerDismissed;
        BannerMessage = assessment.Subheadline;
        CoreCompletedAgoText = prefs.CoreSetupCompletedAt is { } at
            ? $"Core setup finished {OnboardingPreferences.FormatCompletedAgo(at)}"
            : null;

        Steps.Clear();
        foreach (var step in assessment.Steps)
            Steps.Add(OnboardingStepRowViewModel.From(step, prefs, step.Definition.Id == HighlightedStepId));

        Diagnostics.Clear();
        foreach (var d in assessment.Diagnostics)
            Diagnostics.Add(OnboardingDiagnosticViewModel.From(d));

        OptionalSteps.Clear();
        foreach (var def in OnboardingCatalog.OptionalOperateSteps)
        {
            var done = prefs.CompletedOptionalSteps.Contains(def.Id, StringComparer.OrdinalIgnoreCase)
                || OptionalStepSatisfied(def.Id);
            OptionalSteps.Add(new OnboardingStepRowViewModel(
                def.Id, def.Order, def.Title, def.Description, def.WhyItMatters,
                def.FixLabel, def.LearnMoreUrl, def.NavigateSurfaceId,
                done, false, null, null, def.IsRequired, prefs, false));
        }

        WhatsNext.Clear();
        if (assessment.IsComplete)
        {
            foreach (var card in OnboardingCatalog.CompletionCards)
                WhatsNext.Add(new OnboardingWhatsNextCardViewModel(card));
        }

        if (QuickActions.Count == 0)
        {
            foreach (var a in OnboardingCatalog.QuickActions)
                QuickActions.Add(new OnboardingQuickActionViewModel(a.Id, a.Title, a.Description));
        }

        if (Resources.Count == 0)
        {
            foreach (var r in OnboardingCatalog.Resources)
                Resources.Add(new OnboardingResourceLinkViewModel(r));
        }

        Playbooks.Clear();
        foreach (var p in OnboardingCatalog.TroubleshootingPlaybooks)
            Playbooks.Add(new OnboardingPlaybookViewModel(p));
    }

    private bool OptionalStepSatisfied(string id) => id switch
    {
        OnboardingStepIds.TuiConnected => _shell.Execution.PtyConnected,
        _ => false,
    };

    [RelayCommand]
    private async Task PrimaryActionAsync()
    {
        if (!string.IsNullOrEmpty(PrimaryActionStepId))
            await FixStepAsync(PrimaryActionStepId);
    }

    [RelayCommand]
    private async Task FixStepAsync(string? stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId)) return;

        if (stepId is OnboardingStepIds.FirstChat or OnboardingStepIds.FirstDispatch or OnboardingStepIds.TuiConnected)
        {
            NavigateForOptional(stepId);
            return;
        }

        _shell.SetOnboardingToast($"Running: {stepId}…", isError: false);
        try
        {
            await _shell.ApplyOnboardingFixAsync(stepId);
            await RefreshAsync();
            _shell.SetOnboardingToast(_lastAssessment?.IsComplete == true
                ? "Setup complete."
                : "Step updated — see checklist.");
        }
        catch (Exception ex)
        {
            _shell.SetOnboardingToast($"Step failed: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    private async Task RunSmartSetupAsync()
    {
        IsRunningSmartSetup = true;
        try
        {
            await _shell.RunAutomaticOnboardingIfNeededAsync(force: true);
            await RefreshAsync();

            if (_lastAssessment?.IsComplete == true)
                _shell.SetOnboardingToast("Setup complete — you're ready to use Manager Chat.");
            else if (_lastAssessment?.Diagnostics.Any(d => d.Severity == OnboardingDiagnosticSeverity.Error) == true)
                _shell.SetOnboardingToast("Setup could not finish — see Diagnostics below for what to fix.", isError: true);
            else
                _shell.SetOnboardingToast("Setup ran — one or more steps still need attention.");
        }
        finally
        {
            IsRunningSmartSetup = false;
        }
    }

    [RelayCommand]
    private async Task CopyDiagnosticsAsync()
    {
        var ctx = _shell.CreateOnboardingReportContext();
        var text = await OnboardingHealthReport.BuildAsync(ctx);
        if (CopyTextRequested is not null)
            await CopyTextRequested(text);
        _shell.SetOnboardingToast(
            $"Health report copied (also saved to {ClipboardHelper.ReportFilePath}).");
    }

    [RelayCommand]
    private async Task RunQuickActionAsync(string? actionId)
    {
        switch (actionId)
        {
            case "smart_setup":
                await RunSmartSetupAsync();
                break;
            case "wizard":
                if (OpenWizardRequested is not null)
                    await OpenWizardRequested();
                break;
            case "connection":
                if (OpenConnectionRequested is not null)
                    await OpenConnectionRequested();
                break;
            case "workspace":
                _shell.RequestWorkspacePicker();
                break;
            case "tui":
                await _shell.OpenHermesTuiAsync();
                OnboardingPreferences.Load().MarkOptionalStep(OnboardingStepIds.TuiConnected);
                break;
        }
    }

    [RelayCommand]
    private void OpenResource(OnboardingResourceLinkViewModel? link)
    {
        if (link is null) return;
        if (link.IsLocalDoc)
        {
            var baseDir = AppContext.BaseDirectory;
            var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", link.UrlOrPath));
            if (File.Exists(path))
                OpenUrl($"file://{path}");
            else
                _shell.SetOnboardingToast($"Doc not found at {path}", isError: true);
            return;
        }

        OpenUrl(link.UrlOrPath);
    }

    [RelayCommand]
    private async Task OpenWizardAsync()
    {
        if (OpenWizardRequested is not null)
            await OpenWizardRequested();
    }

    [RelayCommand]
    private async Task OpenConnectionAsync()
    {
        if (OpenConnectionRequested is not null)
            await OpenConnectionRequested();
    }

    [RelayCommand]
    private void NavigateSurface(string? surfaceId)
    {
        if (!string.IsNullOrWhiteSpace(surfaceId))
            NavigateSurfaceRequested?.Invoke(surfaceId);
    }

    [RelayCommand]
    private void DismissCelebration()
    {
        OnboardingCoordinator.MarkCelebrationShown();
        ShowCelebration = false;
    }

    [RelayCommand]
    private async Task ApplyPlaybookFixAsync(string? stepId)
    {
        if (!string.IsNullOrWhiteSpace(stepId))
            await FixStepAsync(stepId);
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // ignore
        }
    }

    private void NavigateForOptional(string stepId)
    {
        switch (stepId)
        {
            case OnboardingStepIds.FirstChat:
                NavigateSurfaceRequested?.Invoke(OnboardingSurfaceIds.Manager);
                break;
            case OnboardingStepIds.FirstDispatch:
                NavigateSurfaceRequested?.Invoke(OnboardingSurfaceIds.Kanban);
                break;
            case OnboardingStepIds.TuiConnected:
                NavigateSurfaceRequested?.Invoke(OnboardingSurfaceIds.Execution);
                _ = _shell.OpenHermesTuiAsync();
                break;
        }
    }
}

public partial class OnboardingStepRowViewModel : ViewModelBase
{
    public string Id { get; }
    public int Order { get; }
    public string Title { get; }
    public string Description { get; }
    public string WhyItMatters { get; }
    public string FixLabel { get; }
    public string? LearnMoreUrl { get; }
    public string? NavigateSurfaceId { get; }
    public bool IsComplete { get; }
    public bool IsBlocked { get; }
    public string? BlockedReason { get; }
    public string? RequiresLabel { get; }
    public bool IsRequired { get; }
    public bool IsHighlighted { get; }
    public string? CompletedAtText { get; }

    public string StatusIcon => IsComplete ? "✓" : IsBlocked ? "⊘" : "○";
    public string OrderLabel => Order.ToString();
    public bool ShowFix => !IsComplete && !IsBlocked;
    public bool ShowBlocked => IsBlocked && !IsComplete;
    public bool ShowLearnMore => !string.IsNullOrWhiteSpace(LearnMoreUrl);
    public bool ShowNavigate => !string.IsNullOrWhiteSpace(NavigateSurfaceId);
    public bool ShowRequires => !string.IsNullOrWhiteSpace(RequiresLabel);
    public bool ShowCompletedAt => CompletedAtText is not null;
    public string RowBorderBrush => IsHighlighted ? "#58a6ff" : "#30363d";

    public static OnboardingStepRowViewModel From(
        OnboardingStepState s,
        OnboardingPreferences prefs,
        bool highlight) =>
        new(
            s.Definition.Id,
            s.Definition.Order,
            s.Definition.Title,
            s.Definition.Description,
            s.Definition.WhyItMatters,
            s.Definition.FixLabel,
            s.Definition.LearnMoreUrl,
            s.Definition.NavigateSurfaceId,
            s.IsComplete,
            s.IsBlocked,
            s.BlockedReason,
            s.RequiresLabel,
            s.Definition.IsRequired,
            prefs,
            highlight);

    public OnboardingStepRowViewModel(
        string id, int order, string title, string description, string whyItMatters,
        string fixLabel, string? learnMoreUrl, string? navigateSurfaceId,
        bool isComplete, bool isBlocked, string? blockedReason, string? requiresLabel,
        bool isRequired, OnboardingPreferences prefs, bool highlight)
    {
        Id = id;
        Order = order;
        Title = title;
        Description = description;
        WhyItMatters = whyItMatters;
        FixLabel = fixLabel;
        LearnMoreUrl = learnMoreUrl;
        NavigateSurfaceId = navigateSurfaceId;
        IsComplete = isComplete;
        IsBlocked = isBlocked;
        BlockedReason = blockedReason;
        RequiresLabel = requiresLabel;
        IsRequired = isRequired;
        IsHighlighted = highlight;
        var at = prefs.GetStepCompletedAt(id);
        CompletedAtText = isComplete && at is not null
            ? $"Completed {OnboardingPreferences.FormatCompletedAgo(at.Value)}"
            : null;
    }
}

public record OnboardingDiagnosticViewModel(
    string Code,
    string Title,
    string Detail,
    string SeverityLabel,
    string SeverityColor,
    string? FixStepId)
{
    public bool ShowFix => !string.IsNullOrWhiteSpace(FixStepId);

    public static OnboardingDiagnosticViewModel From(OnboardingDiagnostic d) =>
        new(
            d.Code,
            d.Title,
            d.Detail,
            d.Severity.ToString(),
            d.Severity switch
            {
                OnboardingDiagnosticSeverity.Error => "#f85149",
                OnboardingDiagnosticSeverity.Warning => "#d29922",
                _ => "#8b949e",
            },
            d.FixStepId);
}

public record OnboardingWhatsNextCardViewModel(string Title, string Description, string SurfaceId, string ActionLabel)
{
    public OnboardingWhatsNextCardViewModel(OnboardingWhatsNextCard c)
        : this(c.Title, c.Description, c.SurfaceId, c.ActionLabel) { }
}

public record OnboardingQuickActionViewModel(string Id, string Title, string Description);

public record OnboardingResourceLinkViewModel(string Title, string Description, string UrlOrPath, bool IsLocalDoc)
{
    public OnboardingResourceLinkViewModel(OnboardingResourceLink r)
        : this(r.Title, r.Description, r.UrlOrPath, r.IsLocalDoc) { }
}

public record OnboardingPlaybookViewModel(
    string Title,
    string Summary,
    IReadOnlyList<string> Steps,
    string? RelatedStepId)
{
    public bool ShowFix => !string.IsNullOrWhiteSpace(RelatedStepId);

    public OnboardingPlaybookViewModel(OnboardingPlaybook p)
        : this(p.Title, p.Summary, p.Steps, p.RelatedStepId) { }
}
