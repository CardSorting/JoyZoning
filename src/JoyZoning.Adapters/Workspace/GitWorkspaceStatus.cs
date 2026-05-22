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
        if (index == 'M' || workTree == 'M' || index == 'U' || workTree == 'U')
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
