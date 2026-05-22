using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Views;

public partial class HermesConnectionWindow : Window
{
    private readonly HermesConnectionViewModel _vm;
    private readonly Func<Task<string?>>? _browseInstall;

    public HermesConnectionWindow(HermesConnectionViewModel viewModel, Func<Task<string?>>? browseInstall = null)
    {
        InitializeComponent();
        _vm = viewModel;
        _browseInstall = browseInstall;
        DataContext = viewModel;
        BrowseInstallButton.IsVisible = browseInstall is not null;
        BrowseInstallButton.Click += async (_, _) =>
        {
            if (_browseInstall is null) return;
            var picked = await _browseInstall();
            if (!string.IsNullOrWhiteSpace(picked))
                _vm.InstallRoot = picked;
        };
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HermesConnectionViewModel.PathsValid)
                or nameof(HermesConnectionViewModel.PathValidationMessage))
                UpdatePathValidationColor();
        };
        Loaded += async (_, _) =>
        {
            await viewModel.LoadAsync();
            UpdatePathValidationColor();
        };
    }

    private void UpdatePathValidationColor()
    {
        PathValidationText.Foreground = new SolidColorBrush(
            _vm.PathsValid ? Color.Parse("#3fb950") : Color.Parse("#f85149"));
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
