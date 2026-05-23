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
    private string? _lastFailedText;

    public ObservableCollection<ChatLineViewModel> Messages { get; } = new();
    public ObservableCollection<ParsedTaskSuggestion> SuggestedTasks { get; } = new();
    public ObservableCollection<ChatCommandChipViewModel> CommandChips { get; } = new();

    [ObservableProperty]
    private string _composerText = string.Empty;

    [ObservableProperty]
    private Guid? _sessionId;

    [ObservableProperty]
    private string _sessionDisplayName = "No workspace session";

    [ObservableProperty]
    private string? _workspaceRoot;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private bool _sendInflight;

    [ObservableProperty]
    private string _connectionLabel = "Connecting…";

    [ObservableProperty]
    private string? _lastHermesReply;

    public bool CanCreateTaskFromReply =>
        !string.IsNullOrWhiteSpace(LastHermesReply) && SessionId.HasValue;

    partial void OnLastHermesReplyChanged(string? value) =>
        OnPropertyChanged(nameof(CanCreateTaskFromReply));

    partial void OnSessionIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(CanCreateTaskFromReply));
        OnPropertyChanged(nameof(ShowOnboardingNudge));
        OnPropertyChanged(nameof(ShowEmptyChat));
        OnPropertyChanged(nameof(Placeholder));
    }

    public string Placeholder =>
        ShowOnboardingNudge
            ? "Open or create a workspace to start chatting with Hermes…"
            : "Ask Hermes to fix a bug, explain the repo, or start a bounded YOLO task…";

    public bool ShowOnboardingNudge => !SessionId.HasValue;

    public bool HasMessages => Messages.Count > 0;

    public bool ShowEmptyChat => !ShowOnboardingNudge && !HasMessages && !IsStreaming;

    public bool ShowConnectHermesNudge => ShowOnboardingNudge && !_shell.HermesFullyReady;

    public string OnboardingNudgeMessage =>
        "Open or create a workspace to start chatting with Hermes.";

    public ManagerChatViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
        Messages.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMessages));
            OnPropertyChanged(nameof(ShowEmptyChat));
        };

        CommandChips.Add(new ChatCommandChipViewModel(
            "Explain this workspace",
            "Explain this workspace — structure, key modules, and how work flows through JoyZoning."));
        CommandChips.Add(new ChatCommandChipViewModel(
            "Start bounded YOLO",
            "Start a bounded YOLO task: propose one small, verifiable change with clear acceptance criteria."));
        CommandChips.Add(new ChatCommandChipViewModel(
            "Show blocked workers",
            "Show blocked workers and why each one needs attention."));
        CommandChips.Add(new ChatCommandChipViewModel(
            "Summarize latest changes",
            "Summarize the latest workspace changes and what still needs review."));

        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.HermesFullyReady)
                or nameof(MainWindowViewModel.ActiveSessionId)
                or nameof(MainWindowViewModel.ProjectName)
                or nameof(MainWindowViewModel.WorkspaceRoot)
                or nameof(MainWindowViewModel.StatusLine))
            {
                SessionDisplayName = shell.ProjectName;
                WorkspaceRoot = shell.WorkspaceRoot;
                ConnectionLabel = shell.StatusLine;
                OnPropertyChanged(nameof(ShowOnboardingNudge));
                OnPropertyChanged(nameof(ShowConnectHermesNudge));
                OnPropertyChanged(nameof(OnboardingNudgeMessage));
                OnPropertyChanged(nameof(Placeholder));
                OnPropertyChanged(nameof(ShowEmptyChat));
            }
        };
    }

    public void BindSession(Guid? sessionId, string displayName, string? workspaceRoot)
    {
        SessionId = sessionId;
        SessionDisplayName = displayName;
        WorkspaceRoot = workspaceRoot;
    }

    public void SetConnectionLabel(string label) => ConnectionLabel = label;

    partial void OnIsStreamingChanged(bool value) =>
        OnPropertyChanged(nameof(ShowEmptyChat));

    [RelayCommand]
    private void OpenWorkspaceFromNudge() => _shell.RequestWorkspacePicker();

    [RelayCommand]
    private async Task ConnectHermesFromNudgeAsync() =>
        await _shell.ConnectHermesCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task SendChipAsync(ChatCommandChipViewModel? chip)
    {
        if (chip is null || IsStreaming || SendInflight) return;
        ComposerText = chip.Prompt;
        await SendAsync();
    }

    [RelayCommand]
    private void OpenKanbanFromChat() =>
        _shell.RequestNavigateSurface(OnboardingSurfaceIds.Kanban);

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(ComposerText) || IsStreaming || SendInflight) return;

        Messages.Add(new ChatLineViewModel("You", ComposerText, ChatLineKind.User));
        var text = ComposerText;
        ComposerText = string.Empty;
        _lastFailedText = text;

        if (!SessionId.HasValue)
        {
            Messages.Add(new ChatLineViewModel("System",
                "Open or create a workspace to start chatting with Hermes.", ChatLineKind.System));
            return;
        }

        _streamingLine = new ChatLineViewModel("Hermes", "", ChatLineKind.Assistant) { IsStreaming = true };
        Messages.Add(_streamingLine);
        IsStreaming = true;
        SendInflight = true;

        var result = await AppServices.ControlPlane.SendManagerMessageAsync(SessionId.Value, text);
        if (result is null)
        {
            CompleteStreaming();
            Messages.Add(new ChatLineViewModel("System",
                "Failed to reach control plane. Is it running on :9470?", ChatLineKind.Error,
                retryable: true));
        }
    }

    [RelayCommand]
    private async Task RetryLastSendAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastFailedText)) return;
        ComposerText = _lastFailedText;
        await SendAsync();
    }

    public void AppendDelta(string delta)
    {
        ConnectionLabel = "Live · streaming";
        if (_streamingLine is null)
        {
            _streamingLine = new ChatLineViewModel("Hermes", delta, ChatLineKind.Assistant) { IsStreaming = true };
            Messages.Add(_streamingLine);
            IsStreaming = true;
            SendInflight = true;
        }
        else
        {
            _streamingLine.Text += delta;
        }
    }

    public void CompleteStreaming()
    {
        if (_streamingLine is not null)
        {
            _streamingLine.IsStreaming = false;
            if (_streamingLine is { Text: { Length: > 0 } text })
            {
                LastHermesReply = text;
                RefreshSuggestedTasks();
                _shell.MarkOptionalOnboardingStep(OnboardingStepIds.FirstChat);
                _ = _shell.OnboardingHub.RefreshAsync();
            }
        }

        IsStreaming = false;
        SendInflight = false;
        _streamingLine = null;
        OnPropertyChanged(nameof(CanCreateTaskFromReply));
    }

    public void HandleJoyEvent(JoyEventMessage evt)
    {
        var card = MapJoyEventToCard(evt);
        if (card is not null)
            Messages.Add(card);
    }

    public void AddStatusCard(string kind, string title, string content, Guid? taskId = null)
    {
        Messages.Add(new ChatLineViewModel("System", content, ChatLineKind.StatusCard)
        {
            StatusCardKind = kind,
            CardTitle = title,
            TaskId = taskId,
        });
    }

    private static ChatLineViewModel? MapJoyEventToCard(JoyEventMessage evt)
    {
        return evt.Type switch
        {
            "task.created" => new ChatLineViewModel("System", evt.Type, ChatLineKind.StatusCard)
            {
                StatusCardKind = "task_started",
                CardTitle = "Task started",
                TaskId = evt.CorrelationId == Guid.Empty ? null : evt.CorrelationId,
            },
            "dietcode.execution.started" => new ChatLineViewModel("System", "Worker dispatched.", ChatLineKind.StatusCard)
            {
                StatusCardKind = "worker_dispatched",
                CardTitle = "Worker dispatched",
                TaskId = evt.CorrelationId == Guid.Empty ? null : evt.CorrelationId,
            },
            "verification.report.attached" => new ChatLineViewModel("System", evt.Type, ChatLineKind.StatusCard)
            {
                StatusCardKind = evt.Type.Contains("fail", StringComparison.OrdinalIgnoreCase)
                    ? "verification_failed"
                    : "verification_passed",
                CardTitle = "Verification update",
                TaskId = evt.CorrelationId == Guid.Empty ? null : evt.CorrelationId,
            },
            "hermes.approval.requested" => new ChatLineViewModel("System",
                "Hermes requested operator approval.", ChatLineKind.Escalation)
            {
                StatusCardKind = "needs_review",
                CardTitle = "Approval requested",
                TaskId = evt.CorrelationId == Guid.Empty ? null : evt.CorrelationId,
            },
            _ => null,
        };
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
            Messages.Add(new ChatLineViewModel("System", $"Created: {task.Title}", ChatLineKind.Tool));
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
            $"Created {created} task(s) from Hermes reply.", ChatLineKind.Tool));
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
                $"Created task: {task.Title}", ChatLineKind.Tool));
            await _shell.Kanban.RefreshAsync();
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        _streamingLine = null;
        IsStreaming = false;
        SendInflight = false;
        Messages.Clear();
        ComposerText = string.Empty;
    }
}

public enum ChatLineKind
{
    User,
    Assistant,
    System,
    Tool,
    Error,
    Escalation,
    StatusCard,
}

public partial class ChatLineViewModel : ObservableObject
{
    public ChatLineViewModel(string speaker, string text, ChatLineKind kind, bool retryable = false)
    {
        Speaker = speaker;
        _text = text;
        Kind = kind;
        Retryable = retryable;
    }

    public string Speaker { get; }
    public ChatLineKind Kind { get; }
    public bool Retryable { get; }

    public bool IsOperator => Kind == ChatLineKind.User;
    public bool IsSystem => Kind == ChatLineKind.System;
    public bool IsAssistant => Kind == ChatLineKind.Assistant;
    public bool IsError => Kind == ChatLineKind.Error;
    public bool IsTool => Kind == ChatLineKind.Tool;
    public bool IsEscalation => Kind == ChatLineKind.Escalation;
    public bool IsStatusCard => Kind == ChatLineKind.StatusCard;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string? _statusCardKind;

    [ObservableProperty]
    private string? _cardTitle;

    [ObservableProperty]
    private Guid? _taskId;
}

public record ChatCommandChipViewModel(string Label, string Prompt);
