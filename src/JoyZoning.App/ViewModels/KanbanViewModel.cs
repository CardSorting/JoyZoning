using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;
using JoyZoning.App.Services.Onboarding;
using JoyZoning.Domain.Enums;

namespace JoyZoning.App.ViewModels;

public partial class KanbanViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;

    private static readonly (string Label, WorkTaskStatus Status)[] ColumnMap =
    {
        ("Backlog", WorkTaskStatus.Backlog),
        ("Planned", WorkTaskStatus.Planned),
        ("In Progress", WorkTaskStatus.InProgress),
        ("Needs Approval", WorkTaskStatus.NeedsApproval),
        ("Verifying", WorkTaskStatus.Verifying),
        ("Blocked", WorkTaskStatus.Blocked),
        ("Complete", WorkTaskStatus.Complete),
    };

    public ObservableCollection<KanbanColumnViewModel> Columns { get; } = new();

    [ObservableProperty]
    private Guid? _sessionId;

    [ObservableProperty]
    private string _hint = "Select a card, then use Move / Dispatch.";

    [ObservableProperty]
    private bool _showHermesBanner;

    [ObservableProperty]
    private string _hermesBannerMessage = "";

    [ObservableProperty]
    private KanbanCardViewModel? _selectedCard;

    [ObservableProperty]
    private bool _approveCriticalDispatch;

    [ObservableProperty]
    private bool _showCriticalApproval;

    public void UpdateHermesBanner(bool hermesReady, bool globalSetupBannerVisible)
    {
        ShowHermesBanner = !hermesReady && SessionId.HasValue;
        HermesBannerMessage = globalSetupBannerVisible
            ? "Hermes is not fully connected — use the setup banner above or Hermes → Connect Hermes."
            : "Connect Hermes to import and sync tasks with your Hermes kanban board.";
    }

    public KanbanViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
        foreach (var (label, status) in ColumnMap)
            Columns.Add(new KanbanColumnViewModel(label, status));
    }

    public Task RefreshOnUiAsync() =>
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(RefreshAsync);

    public async Task RefreshAsync()
    {
        foreach (var col in Columns)
            col.Cards.Clear();

        if (!SessionId.HasValue)
        {
            UpdateHermesBanner(false, false);
            return;
        }

        UpdateHermesBanner(
            _shell.HermesFullyReady,
            _shell.ShowSetupBanner);

        var tasks = await AppServices.ControlPlane.ListTasksAsync(SessionId.Value);
        foreach (var task in tasks)
        {
            var col = Columns.FirstOrDefault(c => c.Status == task.Status);
            col?.Cards.Add(new KanbanCardViewModel(
                task.Id, task.Title, task.Agent.ToString(), task.Risk.ToString(), task.Status));
        }

        var sync = await AppServices.ControlPlane.GetKanbanSyncStatusAsync();
        var syncNote = sync?.Message is { Length: > 0 } m ? $" · {m}" : "";
        Hint = $"{tasks.Count} task(s) loaded{syncNote}";
    }

    [RelayCommand]
    public async Task RefreshBoardAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task ImportFromHermesAsync()
    {
        if (!SessionId.HasValue)
        {
            Hint = "Open a workspace first.";
            return;
        }

        var dash = await AppServices.ControlPlane.GetDashboardStatusAsync();
        if (dash?.Ready != true)
        {
            Hint = "Connect dashboard first (Hermes → Connection…).";
            return;
        }

        var result = await AppServices.ControlPlane.ImportKanbanAsync(SessionId.Value);
        Hint = result?.Message ?? "Import failed — try Hermes → Connection… → Connect dashboard.";
        await RefreshAsync();
    }

    partial void OnSelectedCardChanged(KanbanCardViewModel? value)
    {
        ShowCriticalApproval = value?.Risk == nameof(RiskLevel.Critical);
        if (!ShowCriticalApproval)
            ApproveCriticalDispatch = false;

        _ = _shell.RefreshWorkspaceForTaskAsync(value?.Id);
    }

    [RelayCommand]
    private async Task MoveSelectedAsync(string statusName)
    {
        if (SelectedCard is null || !Enum.TryParse<WorkTaskStatus>(statusName, out var status))
            return;

        if (status == WorkTaskStatus.Complete)
        {
            var merge = await AppServices.ControlPlane.MergeLeaseDetailedAsync(SelectedCard.Id);
            Hint = merge.Success
                ? $"Merged: {SelectedCard.Title}"
                : FormatApiError(merge.Error);
            await RefreshAsync();
            return;
        }

        var result = await AppServices.ControlPlane.UpdateTaskStatusDetailedAsync(SelectedCard.Id, status);
        Hint = result.Success ? $"Moved to {status}" : FormatApiError(result.Error);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RevokeLeaseAsync()
    {
        if (SelectedCard is null) return;

        var result = await AppServices.ControlPlane.RevokeLeaseDetailedAsync(SelectedCard.Id);
        Hint = result.Success ? $"Lease revoked: {SelectedCard.Title}" : FormatApiError(result.Error);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DispatchSelectedAsync()
    {
        if (SelectedCard is null) return;

        if (SelectedCard.Risk == nameof(RiskLevel.Critical) && !ApproveCriticalDispatch)
        {
            Hint = "Critical card — check “Approve critical dispatch” before dispatching.";
            return;
        }

        var result = await AppServices.ControlPlane.DispatchTaskDetailedAsync(
            SelectedCard.Id,
            new DispatchOptions(ApproveCriticalDispatch));

        Hint = result.Success
            ? $"Dispatched: {SelectedCard.Title}"
            : FormatApiError(result.Error);

        _shell.DietCodeStatus = result.Success ? "Running" : "Error";
        if (result.Success)
        {
            _shell.MarkOptionalOnboardingStep(OnboardingStepIds.FirstDispatch);
            _ = _shell.OnboardingHub.RefreshAsync();
            await _shell.RefreshWorkspaceForTaskAsync(SelectedCard.Id);
        }

        await RefreshAsync();
    }

    private static string FormatApiError(ApiErrorResponse? error) =>
        error is null ? "Request failed." : OperatorApiHints.FormatError(error);
}

public class KanbanColumnViewModel
{
    public KanbanColumnViewModel(string name, WorkTaskStatus status)
    {
        Name = name;
        Status = status;
    }

    public string Name { get; }
    public WorkTaskStatus Status { get; }
    public ObservableCollection<KanbanCardViewModel> Cards { get; } = new();
}

public record KanbanCardViewModel(Guid Id, string Title, string Agent, string Risk, WorkTaskStatus Status);
