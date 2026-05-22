using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JoyZoning.App.Services;
using JoyZoning.App.ViewModels;
using JoyZoning.Domain.Enums;

namespace JoyZoning.App.Views;

public partial class KanbanView : UserControl
{
    private static readonly DataFormat<string> TaskDragDataFormat =
        DataFormat.CreateInProcessFormat<string>("JoyZoning.TaskId");

    public KanbanView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is KanbanViewModel vm)
                await vm.RefreshAsync();
            WireColumnDropTargets();
        };
    }

    private void WireColumnDropTargets()
    {
        foreach (var border in this.GetVisualDescendants().OfType<Border>()
                     .Where(b => b.Name == "KanbanColumn"))
        {
            border.AddHandler(DragDrop.DragOverEvent, OnColumnDragOver);
            border.AddHandler(DragDrop.DropEvent, OnColumnDrop);
        }
    }

    private void OnCardSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is KanbanViewModel vm && sender is ListBox lb)
            vm.SelectedCard = lb.SelectedItem as KanbanCardViewModel;
    }

    private async void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: KanbanCardViewModel card })
            return;

        var item = new DataTransferItem();
        item.Set(TaskDragDataFormat, card.Id.ToString());
        var transfer = new DataTransfer();
        transfer.Add(item);
        await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
    }

    private void OnColumnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(TaskDragDataFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private async void OnColumnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not KanbanViewModel vm
            || sender is not Control { DataContext: KanbanColumnViewModel col })
            return;

        var taskIdRaw = e.DataTransfer.TryGetValue(TaskDragDataFormat);
        if (taskIdRaw is null || !Guid.TryParse(taskIdRaw, out var taskId))
            return;

        await AppServices.ControlPlane.UpdateTaskStatusAsync(taskId, col.Status);
        await vm.RefreshAsync();
    }

    private async void OnNewTask(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not KanbanViewModel vm || !vm.SessionId.HasValue) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        var dialog = new CreateTaskWindow(vm.SessionId.Value);
        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();

        if (dialog.Created)
            await vm.RefreshAsync();
    }

    private void OnMoveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not KanbanViewModel vm) return;
        var item = MoveStatusCombo.SelectedItem as ComboBoxItem;
        var status = item?.Content?.ToString() ?? "Planned";
        vm.MoveSelectedCommand.Execute(status);
    }

    private async void OnConnectHermes(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        var window = top as Window;
        if (window?.DataContext is MainWindowViewModel shell)
            await shell.ConnectHermesCommand.ExecuteAsync(null);
    }
}
