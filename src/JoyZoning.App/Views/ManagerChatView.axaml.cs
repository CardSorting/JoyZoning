using Avalonia.Controls;
using Avalonia.Interactivity;
using JoyZoning.App.Services;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Views;

public partial class ManagerChatView : UserControl
{
    public ManagerChatView() => InitializeComponent();

    private async void OnSuggestedCreateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ParsedTaskSuggestion suggestion })
            return;
        if (DataContext is not ManagerChatViewModel vm)
            return;

        await vm.CreateSuggestedTaskCommand.ExecuteAsync(suggestion);
    }
}
