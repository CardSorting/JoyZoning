using Avalonia.Controls;
using Avalonia.Media;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Views;

public partial class SetupWizardWindow : Window
{
    private SetupWizardViewModel? _vm;

    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;
        viewModel.WizardFinished += completed =>
        {
            DialogResult = completed;
            Close();
        };
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SetupWizardViewModel.CurrentStep)
                or nameof(SetupWizardViewModel.CanGoBack)
                or nameof(SetupWizardViewModel.CanGoNext)
                or nameof(SetupWizardViewModel.ShowConnectStep)
                or nameof(SetupWizardViewModel.ShowWorkspaceStep)
                or nameof(SetupWizardViewModel.HermesReady)
                or nameof(SetupWizardViewModel.ShowFinishWithoutHermes)
                or nameof(SetupWizardViewModel.PathsValid)
                or nameof(SetupWizardViewModel.PathValidationMessage)
                or nameof(SetupWizardViewModel.HasRecentWorkspace))
                UpdateStepUi();
        };
        Loaded += async (_, _) =>
        {
            await viewModel.InitializeAsync();
            UpdateStepUi();
        };
    }

    public bool? DialogResult { get; private set; }

    private void UpdateStepUi()
    {
        if (_vm is null) return;

        StepWelcome.IsVisible = _vm.CurrentStep == 0;
        StepPaths.IsVisible = _vm.CurrentStep == 1;
        StepConnect.IsVisible = _vm.CurrentStep == 2;
        StepWorkspace.IsVisible = _vm.CurrentStep == 3;

        BackButton.IsVisible = _vm.CanGoBack;
        NextButton.IsVisible = _vm.CanGoNext;
        NextButton.IsEnabled = _vm.CanGoNext;
        ConnectButton.IsVisible = _vm.ShowConnectStep;
        WorkspaceButton.IsVisible = _vm.ShowWorkspaceStep;
        FinishButton.IsVisible = _vm.ShowWorkspaceStep;

        ChecklistPanel.IsVisible = _vm.Checklist.Count > 0;

        HermesReadyText.Text = _vm.HermesReady
            ? "✓ Hermes dashboard connected"
            : "○ Click Connect Hermes to continue";

        PathValidationText.Foreground = new SolidColorBrush(
            _vm.PathsValid ? Color.Parse("#3fb950") : Color.Parse("#f85149"));
    }
}
