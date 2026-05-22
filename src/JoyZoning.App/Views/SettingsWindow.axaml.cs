using Avalonia.Controls;
using Avalonia.Interactivity;
using JoyZoning.App.Services;
using JoyZoning.App.Services.Onboarding;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Views;

public partial class SettingsWindow : Window
{
    private HermesConnectionViewModel? _hermesConnection;
    private MainWindowViewModel? _shell;

    public bool Saved { get; private set; }

    public Action? OpenGettingStartedRequested { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    public void SetHermesConnection(HermesConnectionViewModel vm) => _hermesConnection = vm;

    public void SetShell(MainWindowViewModel shell) => _shell = shell;

    private async Task LoadAsync()
    {
        var s = await AppServices.ControlPlane.GetSettingsAsync();
        if (s is null) return;

        InstallRootBox.Text = s.InstallRoot;
        ApiUrlBox.Text = s.ApiBaseUrl;
        DashboardUrlBox.Text = s.DashboardBaseUrl;
        ProfileBox.Text = s.Profile;
        TokenBox.Text = s.DashboardSessionToken;
        AutoSyncBox.IsChecked = s.AutoSyncEnabled;
        IntervalBox.Text = s.AutoSyncIntervalSeconds.ToString();
        var prefs = OnboardingPreferences.Load();
        AutoSetupOnLaunchBox.IsChecked = prefs.AutoSetupOnLaunch;
        AutoInstallHermesBox.IsChecked = prefs.AutoInstallHermesStack;
        UseSampleWorkspaceBox.IsChecked = prefs.UseSampleWorkspaceWhenNeeded;
        AutoConnectHermesBox.IsChecked = prefs.AutoConnectHermesOnStartup;

        var dash = await AppServices.ControlPlane.GetDashboardStatusAsync();
        var summary = dash?.Ready == true
            ? $"Dashboard connected at {dash.DashboardUrl}"
            : dash?.Reachable == true
                ? "Dashboard running — use Connection to acquire token"
                : "Dashboard not running — use Connection to start it";
        ConnectionSummary.Text = summary;
        DashboardStatus.Text = dash?.Message ?? "";
    }

    private async void OnOpenConnection(object? sender, RoutedEventArgs e)
    {
        var vm = _hermesConnection ?? new HermesConnectionViewModel();
        async Task<string?> BrowseInstall() =>
            await FolderPickerHelper.PickFolderAsync(this, "Select diet-hermes folder", vm.InstallRoot);
        var dialog = new HermesConnectionWindow(vm, BrowseInstall);
        await dialog.ShowDialog(this);
        await LoadAsync();
    }

    private void OnResetOnboarding(object? sender, RoutedEventArgs e)
    {
        OnboardingPreferences.ResetAll();
        AutoSetupOnLaunchBox.IsChecked = true;
        AutoInstallHermesBox.IsChecked = true;
        UseSampleWorkspaceBox.IsChecked = true;
        AutoConnectHermesBox.IsChecked = true;
    }

    private void OnOpenGettingStarted(object? sender, RoutedEventArgs e)
    {
        OpenGettingStartedRequested?.Invoke();
        Close();
    }

    private async void OnCopyDiagnostics(object? sender, RoutedEventArgs e)
    {
        if (_shell is null) return;
        var text = await OnboardingHealthReport.BuildAsync(_shell.CreateOnboardingReportContext());
        await ClipboardHelper.SetTextAsync(TopLevel.GetTopLevel(this), text);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        var interval = int.TryParse(IntervalBox.Text, out var sec) ? Math.Clamp(sec, 30, 3600) : 120;
        var dash = await AppServices.ControlPlane.GetDashboardStatusAsync();

        var settings = new ControlPlaneClient.AppSettings(
            InstallRootBox.Text ?? "",
            ApiUrlBox.Text ?? "",
            DashboardUrlBox.Text ?? "",
            ProfileBox.Text ?? "",
            TokenBox.Text ?? "",
            dash?.Reachable ?? false,
            AutoSyncBox.IsChecked == true,
            interval);

        await AppServices.ControlPlane.SaveSettingsAsync(settings);

        var prefs = OnboardingPreferences.Load();
        prefs.AutoSetupOnLaunch = AutoSetupOnLaunchBox.IsChecked == true;
        prefs.AutoInstallHermesStack = AutoInstallHermesBox.IsChecked == true;
        prefs.UseSampleWorkspaceWhenNeeded = UseSampleWorkspaceBox.IsChecked == true;
        prefs.AutoConnectHermesOnStartup = AutoConnectHermesBox.IsChecked == true;
        prefs.Save();

        Saved = true;
        if (_hermesConnection is not null)
            await _hermesConnection.LoadAsync();
        Close();
    }
}
