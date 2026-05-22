using Avalonia.Controls;
using Avalonia.Interactivity;
using JoyZoning.App.Services;
using JoyZoning.Domain.Enums;

namespace JoyZoning.App.Views;

public partial class CreateTaskWindow : Window
{
    private readonly Guid _sessionId;

    public CreateTaskWindow(Guid sessionId)
    {
        _sessionId = sessionId;
        InitializeComponent();
    }

    public bool Created { get; private set; }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnCreate(object? sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            TitleBox.Focus();
            return;
        }

        var description = DescriptionBox.Text?.Trim() ?? "";
        var agentItem = AgentBox.SelectedItem as ComboBoxItem;
        var agentName = agentItem?.Content?.ToString() ?? "DietCode";
        var agent = Enum.TryParse<AgentKind>(agentName, out var parsed) ? parsed : AgentKind.DietCode;

        var task = await AppServices.ControlPlane.CreateTaskAsync(_sessionId, title, description, agent);
        if (task is null) return;

        Created = true;
        Close(true);
    }
}
