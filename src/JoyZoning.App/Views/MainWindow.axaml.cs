using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JoyZoning.App.Services;
using JoyZoning.App.Services.Onboarding;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainWindowViewModel vm)
        {
            WireOnboardingHub(vm);
            vm.WorkspacePickerRequested += async () => await PickAndOpenWorkspaceAsync(vm);
            vm.NavigateSurfaceRequested += surfaceId => NavigateToSurface(vm, surfaceId);
            Loaded += OnLoadedAsync;
        }
    }

    private void WireOnboardingHub(MainWindowViewModel vm)
    {
        vm.OnboardingHub.OpenWizardRequested += () => ShowSetupWizardAsync();
        vm.OnboardingHub.OpenConnectionRequested += () => ShowHermesConnectionAsync();
        vm.OnboardingHub.NavigateSurfaceRequested += surfaceId => NavigateToSurface(vm, surfaceId);
        vm.OnboardingHub.CopyTextRequested += text => CopyToClipboardAsync(text);
    }

    private async Task CopyToClipboardAsync(string text) =>
        await ClipboardHelper.SetTextAsync(TopLevel.GetTopLevel(this), text);

    private async void OnLoadedAsync(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        await vm.RunAutomaticOnboardingIfNeededAsync();

        if (vm.PendingWorkspacePicker)
            await PickAndOpenWorkspaceAsync(vm);

        if (!vm.IsAutoSettingUp)
        {
            await vm.CheckInterruptedExecutionsAsync();
            if (vm.PendingInterruptedCount > 0)
                await ShowResumeDialogAsync();
        }

        if (vm.IsOperatorReady())
            NavigateToSurface(vm, OnboardingSurfaceIds.Manager);
        else if (vm.ShouldLandOnGettingStarted())
            NavigateToSurface(vm, OnboardingSurfaceIds.GettingStarted);
        else
            NavigateToSurface(vm, OnboardingSurfaceIds.Manager);
    }

    private async Task ShowSetupWizardAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        async Task OpenWorkspace() => await PickAndOpenWorkspaceAsync(vm);

        async Task<string?> BrowseInstall() =>
            await FolderPickerHelper.PickFolderAsync(this, "Select diet-hermes folder", vm.HermesConnection.InstallRoot);

        var wizardVm = new SetupWizardViewModel(vm, vm.HermesConnection, OpenWorkspace, BrowseInstall);
        var wizard = new SetupWizardWindow(wizardVm);
        await wizard.ShowDialog(this);
        await vm.RefreshStatusCommand.ExecuteAsync(null);
        vm.UpdateOnboardingUi();
        NavigateToSurface(vm, OnboardingSurfaceIds.GettingStarted);
    }

    private async Task ShowHermesConnectionAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        async Task<string?> BrowseInstall() =>
            await FolderPickerHelper.PickFolderAsync(this, "Select diet-hermes folder", vm.HermesConnection.InstallRoot);

        var dialog = new HermesConnectionWindow(vm.HermesConnection, BrowseInstall);
        await dialog.ShowDialog(this);
        await vm.RefreshStatusCommand.ExecuteAsync(null);
        vm.UpdateOnboardingUi();
    }

    private async Task PickAndOpenWorkspaceAsync(MainWindowViewModel vm)
    {
        var prefs = OnboardingPreferences.Load();

        if (prefs.UseSampleWorkspaceWhenNeeded)
        {
            var useSample = await ShowWorkspaceChoiceAsync();
            if (useSample == WorkspaceChoice.Sample)
            {
                await vm.UseSampleWorkspaceCommand.ExecuteAsync(null);
                vm.PendingWorkspacePicker = false;
                return;
            }

            if (useSample == WorkspaceChoice.Cancel)
                return;
        }

        var picked = await FolderPickerHelper.PickFolderAsync(this, "Choose your project folder");
        if (!string.IsNullOrWhiteSpace(picked))
        {
            await vm.OpenWorkspaceCommand.ExecuteAsync(picked);
            vm.PendingWorkspacePicker = false;
            return;
        }

        var dialog = new OpenWorkspaceWindow();
        await dialog.ShowDialog(this);
        if (!string.IsNullOrEmpty(dialog.SelectedPath))
        {
            await vm.OpenWorkspaceCommand.ExecuteAsync(dialog.SelectedPath);
            vm.PendingWorkspacePicker = false;
            return;
        }

        if (prefs.UseSampleWorkspaceWhenNeeded)
            await vm.UseSampleWorkspaceCommand.ExecuteAsync(null);
    }

    private async Task<WorkspaceChoice> ShowWorkspaceChoiceAsync()
    {
        var dialog = new Window
        {
            Title = "Open a project",
            Width = 440,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        WorkspaceChoice choice = WorkspaceChoice.Cancel;

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "JoyZoning needs a project folder for chat and kanban.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Classes = { "subtitle" },
            Text = "Pick any folder, or use our ready-made sample workspace — no setup knowledge required.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
        var sampleBtn = new Button { Content = "Use sample workspace" };
        sampleBtn.Click += (_, _) => { choice = WorkspaceChoice.Sample; dialog.Close(); };
        var pickBtn = new Button { Content = "Choose folder…" };
        pickBtn.Click += (_, _) => { choice = WorkspaceChoice.PickFolder; dialog.Close(); };
        buttons.Children.Add(sampleBtn);
        buttons.Children.Add(pickBtn);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
        return choice;
    }

    private enum WorkspaceChoice
    {
        Cancel,
        Sample,
        PickFolder,
    }

    private async void OnOpenSetupWizard(object? sender, RoutedEventArgs e) =>
        await ShowSetupWizardAsync();

    private async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow();
        if (DataContext is MainWindowViewModel vm)
        {
            dialog.SetHermesConnection(vm.HermesConnection);
            dialog.SetShell(vm);
            dialog.OpenGettingStartedRequested = () => NavigateToSurface(vm, OnboardingSurfaceIds.GettingStarted);
        }

        await dialog.ShowDialog(this);
    }

    private async void OnOpenHermesConnection(object? sender, RoutedEventArgs e) =>
        await ShowHermesConnectionAsync();

    private async void OnResumeExecutions(object? sender, RoutedEventArgs e) =>
        await ShowResumeDialogAsync();

    private async Task ShowResumeDialogAsync()
    {
        var dialog = new ResumeExecutionsWindow();
        await dialog.ShowDialog(this);
    }

    private async void OnOpenWorkspace(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await PickAndOpenWorkspaceAsync(vm);
    }

    private async void OnBannerSmartSetup(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            await vm.RunAutomaticOnboardingIfNeededAsync(force: true);
    }

    private void OnNavGettingStarted(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            NavigateToSurface(vm, OnboardingSurfaceIds.GettingStarted);
    }

    private void OnNavManager(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            NavigateToSurface(vm, OnboardingSurfaceIds.Manager);
    }

    private void OnNavKanban(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            NavigateToSurface(vm, OnboardingSurfaceIds.Kanban);
    }

    private void OnNavExecution(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            NavigateToSurface(vm, OnboardingSurfaceIds.Execution);
    }

    private async void OnOpenTuiTerminal(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        NavigateToSurface(vm, OnboardingSurfaceIds.Execution);
        vm.Execution.SelectedTabIndex = 1;
        await vm.Execution.ConnectDashboardAndTuiCommand.ExecuteAsync(null);
        vm.MarkOptionalOnboardingStep(OnboardingStepIds.TuiConnected);
    }

    private void OnNavWorkspace(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            NavigateToSurface(vm, OnboardingSurfaceIds.Workspace);
    }

    private void OnNavTimeline(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            NavigateToSurface(vm, OnboardingSurfaceIds.Timeline);
    }

    private void OnNavApprovals(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            ShowApprovalsSurface(vm);
    }

    private void ShowApprovalsSurface(MainWindowViewModel vm)
    {
        var view = new UserControl { DataContext = vm.Approvals };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Approvals",
            FontSize = 24,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Foreground = Avalonia.Media.Brushes.White,
            Margin = new Avalonia.Thickness(0, 0, 0, 16),
        });
        panel.Children.Add(new ApprovalsPanel { DataContext = vm.Approvals });
        view.Content = panel;
        MainContent.Content = view;
        vm.OnSurfaceActivated(OnboardingSurfaceIds.Approvals);
    }

    private void NavigateToSurface(MainWindowViewModel vm, string surfaceId)
    {
        vm.OnSurfaceActivated(surfaceId);

        switch (surfaceId)
        {
            case OnboardingSurfaceIds.GettingStarted:
                ShowSurface(vm.OnboardingHub, typeof(OnboardingHubView));
                _ = vm.OnboardingHub.RefreshAsync();
                break;
            case OnboardingSurfaceIds.Manager:
                ShowSurface(vm.ManagerChat, typeof(ManagerChatView));
                break;
            case OnboardingSurfaceIds.Kanban:
                ShowSurface(vm.Kanban, typeof(KanbanView));
                break;
            case OnboardingSurfaceIds.Execution:
                ShowSurface(vm.Execution, typeof(ExecutionViewportView));
                break;
            case OnboardingSurfaceIds.Workspace:
                ShowSurface(vm.Workspace, typeof(WorkspaceView));
                break;
            case OnboardingSurfaceIds.Timeline:
                ShowSurface(vm.Timeline, typeof(TimelineView));
                break;
        }
    }

    private void ShowSurface(object viewModel, Type viewType)
    {
        var view = (Control)Activator.CreateInstance(viewType)!;
        view.DataContext = viewModel;

        if (DataContext is MainWindowViewModel shell)
        {
            var tipHost = FindDescendant<SurfaceTipBar>(view);
            if (tipHost is not null)
                tipHost.DataContext = shell.SurfaceTip;
        }

        MainContent.Content = view;
    }

    private static T? FindDescendant<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        foreach (var child in root.GetVisualChildren().OfType<Control>())
        {
            var found = FindDescendant<T>(child);
            if (found is not null) return found;
        }

        return null;
    }
}
