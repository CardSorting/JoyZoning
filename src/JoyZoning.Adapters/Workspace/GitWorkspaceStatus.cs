using System.Diagnostics;

namespace JoyZoning.Adapters.Workspace;

/// <summary>Parses <c>git status --porcelain</c> into changed-file records.</summary>
public static class GitWorkspaceStatus
{
    public static bool IsGitRepository(string workspaceRoot)
    {
        var gitPath = Path.Combine(workspaceRoot, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    public static async Task<(bool GitSucceeded, IReadOnlyList<ChangedFile> Files)> TryListChangedFilesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        if (!IsGitRepository(workspaceRoot))
            return (false, Array.Empty<ChangedFile>());

        var output = await RunGitAsync(workspaceRoot, "status --porcelain=1 -u", cancellationToken);
        if (output is null)
            return (false, Array.Empty<ChangedFile>());

        return (true, ParsePorcelain(output));
    }

    public static async Task<IReadOnlyList<ChangedFile>> ListChangedFilesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        var (_, files) = await TryListChangedFilesAsync(workspaceRoot, cancellationToken);
        return files;
    }

    public static async Task<GitWorktreeSummary?> TryGetWorktreeSummaryAsync(
        string worktreePath,
        string? mergeTargetRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(worktreePath) || !Directory.Exists(worktreePath))
            return null;

        if (!IsGitRepository(worktreePath))
            return null;

        var head = await RunGitAsync(worktreePath, "rev-parse HEAD", cancellationToken);
        var branch = await RunGitAsync(worktreePath, "rev-parse --abbrev-ref HEAD", cancellationToken);
        var (_, files) = await TryListChangedFilesAsync(worktreePath, cancellationToken);

        string? baseCommit = null;
        if (!string.IsNullOrWhiteSpace(mergeTargetRoot)
            && Directory.Exists(mergeTargetRoot)
            && IsGitRepository(mergeTargetRoot))
        {
            var targetHead = await RunGitAsync(mergeTargetRoot, "rev-parse HEAD", cancellationToken);
            if (!string.IsNullOrWhiteSpace(targetHead) && !string.IsNullOrWhiteSpace(head))
            {
                baseCommit = await RunGitAsync(
                    worktreePath,
                    $"merge-base {targetHead.Trim()} {head.Trim()}",
                    cancellationToken);
            }
        }

        var conflictPaths = files
            .Where(f => string.Equals(f.ChangeKind, "unmerged", StringComparison.OrdinalIgnoreCase)
                        || f.Path.Contains("conflict", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToList();

        var porcelainConflict = await DetectUnmergedFromPorcelainAsync(worktreePath, cancellationToken);
        foreach (var p in porcelainConflict)
        {
            if (!conflictPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                conflictPaths.Add(p);
        }

        var isDirty = files.Count > 0;

        return new GitWorktreeSummary(
            HeadCommit: head?.Trim(),
            BranchName: branch?.Trim(),
            BaseCommit: baseCommit?.Trim(),
            IsDirty: isDirty,
            HasUnmergedConflicts: conflictPaths.Count > 0,
            ChangedPaths: files.Select(f => f.Path).ToList(),
            ConflictPaths: conflictPaths);
    }

    private static async Task<IReadOnlyList<string>> DetectUnmergedFromPorcelainAsync(
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        var output = await RunGitAsync(workspaceRoot, "status --porcelain=1 -u", cancellationToken);
        if (output is null)
            return Array.Empty<string>();

        var conflicts = new List<string>();
        foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 3)
                continue;

            var status = line[..2];
            if (!status.Contains('U', StringComparison.Ordinal) && status is not "AA" and not "DD")
                continue;

            var pathPart = line[2..].TrimStart();
            if (!string.IsNullOrWhiteSpace(pathPart))
                conflicts.Add(pathPart);
        }

        return conflicts;
    }

    public static IReadOnlyList<ChangedFile> ParsePorcelain(string porcelain)
    {
        var results = new List<ChangedFile>();
        foreach (var raw in porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 3)
                continue;

            var status = line[..2];
            var pathPart = line[2..].TrimStart();
            if (string.IsNullOrWhiteSpace(pathPart))
                continue;

            var path = pathPart;
            var arrow = pathPart.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
                path = pathPart[(arrow + 4)..].Trim();

            results.Add(new ChangedFile(path, MapChangeKind(status), null));
        }

        return results;
    }

    internal static string MapChangeKind(string status)
    {
        if (status is "??" or "!!")
            return "untracked";

        var index = status.Length > 0 ? status[0] : ' ';
        var workTree = status.Length > 1 ? status[1] : ' ';

        if (index == 'R' || workTree == 'R')
            return "renamed";
        if (index == 'C' || workTree == 'C')
            return "copied";
        if (index == 'A' || workTree == 'A')
            return "added";
        if (index == 'D' || workTree == 'D')
            return "deleted";
        if (index == 'U' || workTree == 'U' || status is "AA" or "DD")
            return "unmerged";

        if (index == 'M' || workTree == 'M')
            return "modified";

        return "changed";
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
            await proc.WaitForExitAsync(cancellationToken);

            return proc.ExitCode == 0 ? stdout : null;
        }
        catch
        {
            return null;
        }
    }
}
