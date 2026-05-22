using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JoyZoning.App.ViewModels;

public partial class WorkspaceViewModel : ViewModelBase
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("http://127.0.0.1:9470/") };

    public ObservableCollection<string> FileTree { get; } = new();
    public ObservableCollection<string> ChangedFiles { get; } = new();

    [ObservableProperty]
    private string _selectedFile = string.Empty;

    [ObservableProperty]
    private string _diffContent = "Select a changed file to preview.";

    [ObservableProperty]
    private bool _showSplitDiff;

    [ObservableProperty]
    private string _removedLines = "";

    [ObservableProperty]
    private string _addedLines = "";

    [ObservableProperty]
    private string _workspaceRoot = string.Empty;

    [ObservableProperty]
    private Guid? _sessionId;

    [ObservableProperty]
    private Guid? _inspectTaskId;

    [ObservableProperty]
    private string _inspectLabel = "";

    /// <summary>Lease worktree when inspecting a dispatched task; otherwise session workspace.</summary>
    public string EffectiveRoot =>
        !string.IsNullOrWhiteSpace(InspectRoot) ? InspectRoot : WorkspaceRoot;

    public string? InspectRoot { get; private set; }

    public bool ShowInspectLabel => !string.IsNullOrWhiteSpace(InspectLabel);

    [RelayCommand]
    private Task RefreshFromUiAsync() => RefreshAsync();

    public async Task RefreshAsync()
    {
        if (InspectTaskId is { } tid && tid != Guid.Empty)
            await RefreshForTaskAsync(tid);
        else
            await RefreshSessionWorkspaceAsync();
    }

    private async Task RefreshSessionWorkspaceAsync()
    {
        var root = WorkspaceRoot;
        if (string.IsNullOrEmpty(root)) return;

        ClearTaskInspection();
        FileTree.Clear();
        ChangedFiles.Clear();

        var tree = await Http.GetFromJsonAsync<string[]>($"api/workspace/tree?workspaceRoot={Uri.EscapeDataString(root)}");
        if (tree is not null)
            foreach (var p in tree.Take(200))
                FileTree.Add(p);

        var changedUrl = $"api/workspace/changed?workspaceRoot={Uri.EscapeDataString(root)}";
        if (SessionId is { } sid && sid != Guid.Empty)
            changedUrl += $"&sessionId={sid}";
        var changed = await Http.GetFromJsonAsync<ChangedFileDto[]>(changedUrl);
        if (changed is not null)
            foreach (var c in changed)
                ChangedFiles.Add(c.Path);
    }

    public async Task RefreshForTaskAsync(Guid? taskId)
    {
        if (taskId is null || taskId == Guid.Empty)
        {
            ClearTaskInspection();
            await RefreshSessionWorkspaceAsync();
            return;
        }

        InspectTaskId = taskId;
        try
        {
            var url = $"api/tasks/{taskId}/workspace/changed";
            if (SessionId is { } sid && sid != Guid.Empty)
                url += $"?sessionId={sid}";

            var payload = await Http.GetFromJsonAsync<TaskWorkspaceChangedDto>(url);
            if (payload is null)
            {
                ClearTaskInspection();
                await RefreshSessionWorkspaceAsync();
                return;
            }

            InspectRoot = payload.WorkspaceRoot;
            InspectLabel = payload.Inspect == "worktree"
                ? $"Lease worktree · {ShortPath(payload.WorkspaceRoot)}"
                : $"Session workspace · {ShortPath(payload.WorkspaceRoot)}";

            FileTree.Clear();
            ChangedFiles.Clear();

            var tree = await Http.GetFromJsonAsync<string[]>(
                $"api/workspace/tree?workspaceRoot={Uri.EscapeDataString(payload.WorkspaceRoot)}");
            if (tree is not null)
                foreach (var p in tree.Take(200))
                    FileTree.Add(p);

            if (payload.Files is not null)
                foreach (var c in payload.Files)
                    ChangedFiles.Add(c.Path);

            OnPropertyChanged(nameof(EffectiveRoot));
            OnPropertyChanged(nameof(InspectRoot));
        }
        catch
        {
            ClearTaskInspection();
            await RefreshSessionWorkspaceAsync();
        }
    }

    public void ClearTaskInspection()
    {
        InspectTaskId = null;
        InspectRoot = null;
        InspectLabel = "";
        OnPropertyChanged(nameof(EffectiveRoot));
        OnPropertyChanged(nameof(InspectRoot));
        OnPropertyChanged(nameof(ShowInspectLabel));
    }

    partial void OnInspectLabelChanged(string value) => OnPropertyChanged(nameof(ShowInspectLabel));

    partial void OnSelectedFileChanged(string value)
    {
        _ = LoadPreviewAsync(value);
    }

    private async Task LoadPreviewAsync(string relativePath)
    {
        var root = EffectiveRoot;
        if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(root))
        {
            DiffContent = "Select a changed file to preview.";
            return;
        }

        var full = Path.Combine(root, relativePath);
        if (!File.Exists(full))
        {
            DiffContent = $"File not found: {relativePath}";
            return;
        }

        try
        {
            var diffUrl = InspectTaskId is { } taskId && taskId != Guid.Empty
                ? $"api/tasks/{taskId}/workspace/diff?path={Uri.EscapeDataString(relativePath)}"
                : $"api/workspace/diff?workspaceRoot={Uri.EscapeDataString(root)}&path={Uri.EscapeDataString(relativePath)}";
            var diffResponse = await Http.GetAsync(diffUrl);
            if (diffResponse.IsSuccessStatusCode)
            {
                var json = await diffResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                if (json.TryGetProperty("diff", out var diffEl))
                {
                    var raw = diffEl.GetString() ?? "";
                    ApplySplitDiff(raw, relativePath);
                    return;
                }
            }

            ShowSplitDiff = false;

            var text = await File.ReadAllTextAsync(full);
            var lines = text.Split('\n').Take(120).ToArray();
            var numbered = string.Join("\n", lines.Select((l, i) => $"{i + 1,4} | {l.TrimEnd('\r')}"));
            var truncated = text.Split('\n').Length > 120 ? "\n… (truncated)" : "";
            DiffContent = $"── {relativePath} (file preview) ──\n{numbered}{truncated}";
        }
        catch (Exception ex)
        {
            DiffContent = $"Cannot read {relativePath}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenExternal()
    {
        var root = EffectiveRoot;
        if (string.IsNullOrEmpty(SelectedFile) || string.IsNullOrEmpty(root)) return;
        var path = Path.Combine(root, SelectedFile);
        try
        {
            if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", path);
        }
        catch
        {
            // ignore
        }
    }

    private void ApplySplitDiff(string raw, string relativePath)
    {
        var removed = new System.Text.StringBuilder();
        var added = new System.Text.StringBuilder();

        foreach (var line in raw.Split('\n'))
        {
            if (line.StartsWith("---") || line.StartsWith("+++") || line.StartsWith("@@"))
                continue;
            if (line.StartsWith("-"))
                removed.AppendLine(line);
            else if (line.StartsWith("+"))
                added.AppendLine(line);
        }

        if (removed.Length > 0 || added.Length > 0)
        {
            ShowSplitDiff = true;
            RemovedLines = removed.Length > 0 ? removed.ToString() : "(no removals)";
            AddedLines = added.Length > 0 ? added.ToString() : "(no additions)";
            DiffContent = $"── git diff: {relativePath} ──";
            return;
        }

        ShowSplitDiff = false;
        DiffContent = $"── git diff: {relativePath} ──\n{raw}";
    }

    private static string ShortPath(string path)
    {
        if (path.Length <= 48) return path;
        return "…" + path[^45..];
    }

    private record ChangedFileDto(string Path, string ChangeKind, DateTimeOffset? ModifiedAt);

    private record TaskWorkspaceChangedDto(
        Guid TaskId,
        string WorkspaceRoot,
        string Inspect,
        ChangedFileDto[]? Files);
}
