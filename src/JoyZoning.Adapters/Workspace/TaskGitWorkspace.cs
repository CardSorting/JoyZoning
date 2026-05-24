using System.Diagnostics;
using System.Text.Json;

namespace JoyZoning.Adapters.Workspace;

/// <summary>Git operations for external-agent task workspaces.</summary>
public static class TaskGitWorkspace
{
    public static async Task<(bool Succeeded, string? Error)> EnsureBranchAsync(
        string workspacePath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        if (!GitWorkspaceStatus.IsGitRepository(workspacePath))
            return (false, "Workspace is not a git repository.");

        var current = await RunGitAsync(workspacePath, "rev-parse --abbrev-ref HEAD", cancellationToken);
        if (string.Equals(current?.Trim(), branchName, StringComparison.Ordinal))
            return (true, null);

        var exists = await RunGitAsync(workspacePath, $"show-ref --verify --quiet refs/heads/{branchName}", cancellationToken);
        if (exists is not null)
        {
            var checkout = await RunGitAsync(workspacePath, $"checkout {branchName}", cancellationToken);
            return checkout is not null ? (true, null) : (false, $"Failed to checkout branch {branchName}.");
        }

        var create = await RunGitAsync(workspacePath, $"checkout -b {branchName}", cancellationToken);
        return create is not null ? (true, null) : (false, $"Failed to create branch {branchName}.");
    }

    public static async Task<string?> GetCurrentBranchAsync(
        string workspacePath,
        CancellationToken cancellationToken = default) =>
        (await RunGitAsync(workspacePath, "rev-parse --abbrev-ref HEAD", cancellationToken))?.Trim();

    public static async Task<string?> GetHeadCommitAsync(
        string workspacePath,
        CancellationToken cancellationToken = default) =>
        (await RunGitAsync(workspacePath, "rev-parse HEAD", cancellationToken))?.Trim();

    public static async Task<IReadOnlyList<string>> ListChangedFilesAgainstBaseAsync(
        string workspacePath,
        string baseRef,
        CancellationToken cancellationToken = default)
    {
        var output = await RunGitAsync(workspacePath, $"diff --name-only {baseRef}...HEAD", cancellationToken);
        if (string.IsNullOrWhiteSpace(output))
            return Array.Empty<string>();

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    public static (IReadOnlyList<string> Staged, IReadOnlyList<string> Untracked) PartitionChangedFiles(
        IReadOnlyList<ChangedFile> files)
    {
        var staged = new List<string>();
        var untracked = new List<string>();
        foreach (var file in files)
        {
            if (string.Equals(file.ChangeKind, "untracked", StringComparison.OrdinalIgnoreCase))
                untracked.Add(file.Path);
            else
                staged.Add(file.Path);
        }

        return (staged, untracked);
    }

    public static async Task<bool> TryCommitAllAsync(
        string workspacePath,
        string message,
        CancellationToken cancellationToken = default)
    {
        await RunGitAsync(workspacePath, "add -A", cancellationToken);
        var safeMessage = message.Replace("\"", "'", StringComparison.Ordinal);
        var commit = await RunGitAsync(
            workspacePath,
            $"commit -m {safeMessage} --allow-empty",
            cancellationToken);
        return commit is not null;
    }

    public static string SerializeChangedFiles(IReadOnlyList<string> paths) =>
        JsonSerializer.Serialize(paths);

    public static IReadOnlyList<string> DeserializeChangedFiles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static async Task<string?> RunGitAsync(
        string workspaceRoot,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;

            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            return proc.ExitCode == 0 ? stdout : null;
        }
        catch
        {
            return null;
        }
    }
}
