using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JoyZoning.App.Services;
using JoyZoning.App.Services.Onboarding;
using JoyZoning.Domain.Enums;

namespace JoyZoning.App.ViewModels;

public partial class ManagerChatViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private ChatLineViewModel? _streamingLine;

    public ObservableCollection<ChatLineViewModel> Messages { get; } = new();
    public ObservableCollection<ParsedTaskSuggestion> SuggestedTasks { get; } = new();

    [ObservableProperty]
    private string _composerText = string.Empty;

    [ObservableProperty]
    private Guid? _sessionId;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string? _lastHermesReply;

    public bool CanCreateTaskFromReply =>
        !string.IsNullOrWhiteSpace(LastHermesReply) && SessionId.HasValue;

    partial void OnLastHermesReplyChanged(string? value) =>
        OnPropertyChanged(nameof(CanCreateTaskFromReply));

    partial void OnSessionIdChanged(Guid? value) =>
        OnPropertyChanged(nameof(CanCreateTaskFromReply));

    public string Placeholder =>
        ShowOnboardingNudge
            ? "Open a workspace to start chatting with Hermes…"
            : "Talk to Hermes — decompose goals, review status, revise plans…";

    public bool ShowOnboardingNudge => !SessionId.HasValue;

    public bool ShowConnectHermesNudge => ShowOnboardingNudge && !_shell.HermesFullyReady;

    public string OnboardingNudgeMessage =>
        _shell.HermesFullyReady
            ? "Hermes is connected. Open a workspace (Project → Open Workspace or the banner above) to start Manager Chat."
            : "Connect Hermes first (setup banner or Hermes → Connect Hermes), then open a workspace.";

    public ManagerChatViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.HermesFullyReady)
                or nameof(MainWindowViewModel.ActiveSessionId))
            {
                OnPropertyChanged(nameof(ShowOnboardingNudge));
                OnPropertyChanged(nameof(ShowConnectHermesNudge));
                OnPropertyChanged(nameof(OnboardingNudgeMessage));
                OnPropertyChanged(nameof(Placeholder));
            }
        };
    }

    [RelayCommand]
    private void OpenWorkspaceFromNudge() => _shell.RequestWorkspacePicker();

    [RelayCommand]
    private async Task ConnectHermesFromNudgeAsync() =>
        await _shell.ConnectHermesCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(ComposerText)) return;

        Messages.Add(new ChatLineViewModel("You", ComposerText, true));
        var text = ComposerText;
        ComposerText = string.Empty;

        if (!SessionId.HasValue)
        {
            Messages.Add(new ChatLineViewModel("System",
                "Open a workspace first (Project → Open Workspace).", false));
            return;
        }

        _streamingLine = new ChatLineViewModel("Hermes", "", false);
        Messages.Add(_streamingLine);
        IsStreaming = true;

        var result = await AppServices.ControlPlane.SendManagerMessageAsync(SessionId.Value, text);
        if (result is null)
        {
            CompleteStreaming();
            Messages.Add(new ChatLineViewModel("System",
                "Failed to reach control plane. Is it running on :9470?", false));
        }
    }

    public void AppendDelta(string delta)
    {
        if (_streamingLine is null)
        {
            _streamingLine = new ChatLineViewModel("Hermes", delta, false);
            Messages.Add(_streamingLine);
            IsStreaming = true;
        }
        else
        {
            _streamingLine.Text += delta;
        }
    }

    public void CompleteStreaming()
    {
        if (_streamingLine is { Text: { Length: > 0 } text })
        {
            LastHermesReply = text;
            RefreshSuggestedTasks();
            _shell.MarkOptionalOnboardingStep(OnboardingStepIds.FirstChat);
            _ = _shell.OnboardingHub.RefreshAsync();
        }

        IsStreaming = false;
        _streamingLine = null;
        OnPropertyChanged(nameof(CanCreateTaskFromReply));
    }

    [RelayCommand]
    private void ParseSuggestedTasks() => RefreshSuggestedTasks();

    [ObservableProperty]
    private bool _hasSuggestedTasks;

    private void RefreshSuggestedTasks()
    {
        SuggestedTasks.Clear();
        foreach (var item in HermesReplyTaskParser.Parse(LastHermesReply))
            SuggestedTasks.Add(item);
        HasSuggestedTasks = SuggestedTasks.Count > 0;
    }

    [RelayCommand]
    private async Task CreateSuggestedTaskAsync(ParsedTaskSuggestion? suggestion)
    {
        if (!SessionId.HasValue || suggestion is null) return;

        var task = await AppServices.ControlPlane.CreateTaskAsync(
            SessionId.Value, suggestion.Title, suggestion.Description, AgentKind.DietCode);

        if (task is not null)
        {
            Messages.Add(new ChatLineViewModel("System", $"Created: {task.Title}", false));
            await _shell.Kanban.RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task CreateAllSuggestedTasksAsync()
    {
        if (!SessionId.HasValue || SuggestedTasks.Count == 0) return;

        var created = 0;
        foreach (var suggestion in SuggestedTasks.ToList())
        {
            var task = await AppServices.ControlPlane.CreateTaskAsync(
                SessionId.Value, suggestion.Title, suggestion.Description, AgentKind.DietCode);
            if (task is not null) created++;
        }

        Messages.Add(new ChatLineViewModel("System",
            $"Created {created} task(s) from Hermes reply.", false));
        await _shell.Kanban.RefreshAsync();
    }

    [RelayCommand]
    private async Task CreateTaskFromReplyAsync()
    {
        if (!SessionId.HasValue || string.IsNullOrWhiteSpace(LastHermesReply))
            return;

        var body = LastHermesReply.Trim();
        var title = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Task from Hermes";
        if (title.Length > 80)
            title = title[..77] + "...";

        var task = await AppServices.ControlPlane.CreateTaskAsync(
            SessionId.Value, title, body, AgentKind.DietCode);

        if (task is not null)
        {
            Messages.Add(new ChatLineViewModel("System",
                $"Created task: {task.Title}", false));
            await _shell.Kanban.RefreshAsync();
        }
    }
}

public partial class ChatLineViewModel : ObservableObject
{
    public ChatLineViewModel(string speaker, string text, bool isOperator)
    {
        Speaker = speaker;
        _text = text;
        IsOperator = isOperator;
    }

    public string Speaker { get; }
    public bool IsOperator { get; }

    [ObservableProperty]
    private string _text;
}
