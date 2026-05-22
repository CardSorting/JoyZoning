using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Adapters.Workspace;

public class LocalWorkspaceAdapter : IWorkspaceAdapter
{
    private readonly ILogger<LocalWorkspaceAdapter> _logger;

    public LocalWorkspaceAdapter(ILogger<LocalWorkspaceAdapter> logger) => _logger = logger;

    public Task<IReadOnlyList<string>> ListTreeAsync(
        string workspaceRoot,
        int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workspaceRoot))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var results = new List<string>();
        CollectTree(workspaceRoot, workspaceRoot, 0, maxDepth, results, cancellationToken);
        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    public Task<IReadOnlyList<ChangedFile>> ListChangedFilesAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        // MVP: return recently modified files in workspace (last 24h)
        if (!Directory.Exists(workspaceRoot))
            return Task.FromResult<IReadOnlyList<ChangedFile>>(Array.Empty<ChangedFile>());

        var cutoff = DateTime.UtcNow.AddHours(-24);
        var files = Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/.git/", StringComparison.Ordinal) &&
                        !f.Contains("\\.git\\", StringComparison.Ordinal))
            .Select(f => new FileInfo(f))
            .Where(fi => fi.LastWriteTimeUtc >= cutoff)
            .Take(100)
            .Select(fi => new ChangedFile(
                Path.GetRelativePath(workspaceRoot, fi.FullName),
                "modified",
                fi.LastWriteTimeUtc))
            .ToList();

        return Task.FromResult<IReadOnlyList<ChangedFile>>(files);
    }

    public async Task<string?> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task<string?> GetGitDiffAsync(
        string workspaceRoot,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var gitDir = Path.Combine(workspaceRoot, ".git");
        if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
            return null;

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"diff -- \"{relativePath.Replace("\"", "\\\"")}\"",
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
                return string.IsNullOrWhiteSpace(stderr) ? null : $"git diff failed:\n{stderr}";

            if (string.IsNullOrWhiteSpace(stdout))
            {
                var staged = await RunGitAsync(workspaceRoot, $"diff --cached -- \"{relativePath}\"", cancellationToken);
                return string.IsNullOrWhiteSpace(staged) ? "(no unstaged or staged diff)" : staged;
            }

            return stdout.Length > 120_000 ? stdout[..120_000] + "\n… (truncated)" : stdout;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "git diff failed for {Path}", relativePath);
            return null;
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
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc is null) return null;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync(cancellationToken);
        return stdout;
    }

    public void OpenInExternalEditor(string path)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", path);
            else if (OperatingSystem.IsLinux())
                System.Diagnostics.Process.Start("xdg-open", path);
            else if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open {Path} in external editor", path);
        }
    }

    private static void CollectTree(
        string root,
        string current,
        int depth,
        int maxDepth,
        List<string> results,
        CancellationToken cancellationToken)
    {
        if (depth > maxDepth) return;

        foreach (var dir in Directory.EnumerateDirectories(current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(dir);
            if (name.StartsWith('.') || name is "node_modules" or "bin" or "obj")
                continue;

            results.Add(Path.GetRelativePath(root, dir) + "/");
            CollectTree(root, dir, depth + 1, maxDepth, results, cancellationToken);
        }

        if (depth == maxDepth) return;

        foreach (var file in Directory.EnumerateFiles(current).Take(50))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith('.')) continue;
            results.Add(Path.GetRelativePath(root, file));
        }
    }
}
