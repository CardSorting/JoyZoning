using Avalonia.Controls;
using Avalonia.Interactivity;
using JoyZoning.App.Services;

namespace JoyZoning.App.Views;

public partial class ResumeExecutionsWindow : Window
{
    private List<ControlPlaneClient.InterruptedExecution> _items = new();

    public ResumeExecutionsWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _items = (await AppServices.ControlPlane.ListInterruptedExecutionsAsync()).ToList();
        ExecutionList.ItemsSource = _items;
    }

    private async void OnResumeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not ControlPlaneClient.InterruptedExecution item) return;
        await AppServices.ControlPlane.ResumeExecutionAsync(item.Id);
        _items.Remove(item);
        ExecutionList.ItemsSource = null;
        ExecutionList.ItemsSource = _items;
    }

    private async void OnDismissClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not ControlPlaneClient.InterruptedExecution item) return;
        await AppServices.ControlPlane.CancelExecutionAsync(item.Id);
        _items.Remove(item);
        ExecutionList.ItemsSource = null;
        ExecutionList.ItemsSource = _items;
    }

    private async void OnDismissAll(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _items.ToList())
            await AppServices.ControlPlane.CancelExecutionAsync(item.Id);
        _items.Clear();
        ExecutionList.ItemsSource = _items;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
