using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;
using JoyZoning.Domain.Enums;

namespace JoyZoning.App.ViewModels;

public partial class ApprovalsViewModel : ViewModelBase
{
    public ObservableCollection<ApprovalCardViewModel> Pending { get; } = new();

    [ObservableProperty]
    private ApprovalCardViewModel? _selected;

    public Task RefreshOnUiAsync() =>
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(RefreshAsync);

    public async Task RefreshAsync()
    {
        Pending.Clear();
        var items = await AppServices.ControlPlane.ListPendingApprovalsAsync();
        foreach (var item in items)
        {
            Pending.Add(new ApprovalCardViewModel(
                item.Id, item.Command, item.Description, item.Risk, this));
        }
    }

    [RelayCommand]
    public async Task RefreshApprovalsAsync() => await RefreshAsync();
}

public partial class ApprovalCardViewModel : ViewModelBase
{
    private readonly ApprovalsViewModel _parent;

    public ApprovalCardViewModel(
        Guid id, string command, string description, string risk, ApprovalsViewModel parent)
    {
        Id = id;
        Command = command;
        Description = description;
        Risk = risk;
        _parent = parent;
    }

    public Guid Id { get; }
    public string Command { get; }
    public string Description { get; }
    public string Risk { get; }

    [RelayCommand]
    private async Task ApproveOnceAsync() => await ResolveAsync(ApprovalScope.Once);

    [RelayCommand]
    private async Task ApproveSessionAsync() => await ResolveAsync(ApprovalScope.Session);

    [RelayCommand]
    private async Task ApproveForTaskAsync() => await ResolveAsync(ApprovalScope.Task);

    [RelayCommand]
    private async Task DenyAsync() => await ResolveAsync(ApprovalScope.Deny);

    private async Task ResolveAsync(ApprovalScope scope)
    {
        await AppServices.ControlPlane.ResolveApprovalAsync(Id, scope);
        await _parent.RefreshAsync();
    }
}
