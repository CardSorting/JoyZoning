using System.Diagnostics;

namespace JoyZoning.Adapters.Workspace;

public sealed record GitCommandResult(int ExitCode, string StdOut, string StdErr);

public static class GitCommandRunner
{
    public static async Task<GitCommandResult> RunAsync(
        string workingDirectory,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
                return new GitCommandResult(-1, string.Empty, "Failed to start git process.");

            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);
            return new GitCommandResult(proc.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return new GitCommandResult(-1, string.Empty, ex.Message);
        }
    }

    public static async Task<string?> ReadRevAsync(
        string workingDirectory,
        string refSpec,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(workingDirectory, $"rev-parse {refSpec}", cancellationToken);
        return result.ExitCode == 0 ? result.StdOut.Trim() : null;
    }
}
