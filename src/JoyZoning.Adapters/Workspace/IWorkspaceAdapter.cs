namespace JoyZoning.Adapters.Workspace;

public interface IWorkspaceAdapter
{
    Task<IReadOnlyList<string>> ListTreeAsync(string workspaceRoot, int maxDepth = 3, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChangedFile>> ListChangedFilesAsync(string workspaceRoot, CancellationToken cancellationToken = default);
    Task<string?> ReadFileAsync(string path, CancellationToken cancellationToken = default);
    Task<string?> GetGitDiffAsync(string workspaceRoot, string relativePath, CancellationToken cancellationToken = default);
    void OpenInExternalEditor(string path);
}

public sealed record ChangedFile(string Path, string ChangeKind, DateTimeOffset? ModifiedAt);
