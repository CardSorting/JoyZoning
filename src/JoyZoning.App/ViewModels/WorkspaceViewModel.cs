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

    public async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(WorkspaceRoot)) return;

        FileTree.Clear();
        ChangedFiles.Clear();

        var tree = await Http.GetFromJsonAsync<string[]>($"api/workspace/tree?workspaceRoot={Uri.EscapeDataString(WorkspaceRoot)}");
        if (tree is not null)
            foreach (var p in tree.Take(200))
                FileTree.Add(p);

        var changed = await Http.GetFromJsonAsync<ChangedFileDto[]>($"api/workspace/changed?workspaceRoot={Uri.EscapeDataString(WorkspaceRoot)}");
        if (changed is not null)
            foreach (var c in changed)
                ChangedFiles.Add(c.Path);
    }

    partial void OnSelectedFileChanged(string value)
    {
        _ = LoadPreviewAsync(value);
    }

    private async Task LoadPreviewAsync(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(WorkspaceRoot))
        {
            DiffContent = "Select a changed file to preview.";
            return;
        }

        var full = Path.Combine(WorkspaceRoot, relativePath);
        if (!File.Exists(full))
        {
            DiffContent = $"File not found: {relativePath}";
            return;
        }

        try
        {
            var diffUrl =
                $"api/workspace/diff?workspaceRoot={Uri.EscapeDataString(WorkspaceRoot)}&path={Uri.EscapeDataString(relativePath)}";
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
        if (string.IsNullOrEmpty(SelectedFile) || string.IsNullOrEmpty(WorkspaceRoot)) return;
        var path = Path.Combine(WorkspaceRoot, SelectedFile);
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

    private record ChangedFileDto(string Path, string ChangeKind, DateTimeOffset? ModifiedAt);
}
