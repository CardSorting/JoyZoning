using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Views;

public partial class SurfaceTipBar : UserControl
{
    public SurfaceTipBar()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => UpdateLearnMoreVisibility();
    }

    private void UpdateLearnMoreVisibility()
    {
        if (LearnMoreButton is null) return;
        LearnMoreButton.IsVisible = DataContext is SurfaceTipViewModel vm
            && !string.IsNullOrWhiteSpace(vm.LearnMoreUrl);
    }

    private void OnLearnMore(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SurfaceTipViewModel vm && !string.IsNullOrWhiteSpace(vm.LearnMoreUrl))
            Process.Start(new ProcessStartInfo(vm.LearnMoreUrl) { UseShellExecute = true });
    }
}
