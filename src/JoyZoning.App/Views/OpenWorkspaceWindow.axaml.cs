using Avalonia.Controls;
using Avalonia.Interactivity;

namespace JoyZoning.App.Views;

public partial class OpenWorkspaceWindow : Window
{
    public string? SelectedPath { get; private set; }

    public OpenWorkspaceWindow()
    {
        InitializeComponent();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        PathBox.Text = Path.Combine(home, "Downloads");
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        SelectedPath = PathBox.Text?.Trim();
        Close();
    }
}
