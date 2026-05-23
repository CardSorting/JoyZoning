using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JoyZoning.App.Services;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Views;

public partial class ManagerChatView : UserControl
{
  public ManagerChatView()
  {
    InitializeComponent();
    DataContextChanged += (_, _) =>
    {
      if (DataContext is ManagerChatViewModel vm)
      {
        vm.Messages.CollectionChanged += (_, _) => ScrollToBottom();
        vm.PropertyChanged += (_, e) =>
        {
          if (e.PropertyName is nameof(ManagerChatViewModel.IsStreaming))
            ScrollToBottom();
        };
      }
    };
  }

  private void ScrollToBottom()
  {
    MessageScroll?.ScrollToEnd();
  }

  private async void OnSuggestedCreateClick(object? sender, RoutedEventArgs e)
  {
    if (sender is not Button { DataContext: ParsedTaskSuggestion suggestion })
      return;
    if (DataContext is not ManagerChatViewModel vm)
      return;

    await vm.CreateSuggestedTaskCommand.ExecuteAsync(suggestion);
  }

  private async void OnComposerKeyDown(object? sender, KeyEventArgs e)
  {
    if (e.Key != Key.Enter || (e.KeyModifiers & KeyModifiers.Shift) != 0)
      return;

    if (DataContext is not ManagerChatViewModel vm || vm.IsStreaming)
      return;

    e.Handled = true;
    await vm.SendCommand.ExecuteAsync(null);
  }

  private async Task SendChipPrompt(string prompt)
  {
    if (DataContext is not ManagerChatViewModel vm) return;
    vm.ComposerText = prompt;
    await vm.SendCommand.ExecuteAsync(null);
  }

  private async void OnChipExplain(object? sender, RoutedEventArgs e) =>
    await SendChipPrompt("Explain this workspace — structure, key modules, and how work flows through JoyZoning.");

  private async void OnChipYolo(object? sender, RoutedEventArgs e) =>
    await SendChipPrompt("Start a bounded YOLO task: propose one small, verifiable change with clear acceptance criteria.");

  private async void OnChipBlocked(object? sender, RoutedEventArgs e) =>
    await SendChipPrompt("Show blocked workers and why each one needs attention.");

  private async void OnChipChanges(object? sender, RoutedEventArgs e) =>
    await SendChipPrompt("Summarize the latest workspace changes and what still needs review.");
}
