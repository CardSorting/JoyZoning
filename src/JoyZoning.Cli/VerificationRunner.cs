using System.Diagnostics;
using System.Text;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public sealed record CommandRunResult(
    string Command,
    bool Passed,
    int ExitCode,
    string Summary);

public static class VerificationRunner
{
    public static async Task<IReadOnlyList<CommandRunResult>> RunCommandsAsync(
        string workingDirectory,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken = default)
    {
        if (commands.Count == 0)
            throw new CliUsageException("At least one --cmd is required for verify.");

        Directory.CreateDirectory(workingDirectory);
        var results = new List<CommandRunResult>();

        foreach (var cmd in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RunOneAsync(workingDirectory, cmd, cancellationToken));
        }

        return results;
    }

    public static VerificationReport BuildReport(
        Guid cardId,
        Guid sessionId,
        IReadOnlyList<CommandRunResult> runs,
        bool readyForHumanReview = true) =>
        new()
        {
            CardId = cardId,
            SessionId = sessionId,
            CommandsRun = runs.Select(r => new CommandRunSummary
            {
                Command = r.Command,
                Passed = r.Passed,
                Summary = r.Summary,
            }).ToList(),
            ReadyForHumanReview = readyForHumanReview && runs.All(r => r.Passed),
        };

    private static async Task<CommandRunResult> RunOneAsync(
        string workingDirectory,
        string commandLine,
        CancellationToken cancellationToken)
    {
        var isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "/bin/sh",
            Arguments = isWindows ? $"/c \"{commandLine}\"" : $"-c \"{commandLine.Replace("\"", "\\\"")}\"",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var exitCode = process.ExitCode;
        var summary = Summarize(stdout, stderr, exitCode);

        return new CommandRunResult(commandLine, exitCode == 0, exitCode, summary);
    }

    public static string Summarize(string stdout, string stderr, int exitCode)
    {
        var sb = new StringBuilder();
        sb.Append($"exit={exitCode}");
        var tail = Tail(stdout, 1200);
        if (!string.IsNullOrWhiteSpace(tail))
            sb.Append("; stdout: ").Append(tail.Replace('\n', ' ').Trim());
        var errTail = Tail(stderr, 800);
        if (!string.IsNullOrWhiteSpace(errTail))
            sb.Append("; stderr: ").Append(errTail.Replace('\n', ' ').Trim());
        if (sb.Length > 2000)
            return sb.ToString(0, 2000) + "…";
        return sb.ToString();
    }

    private static string Tail(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        text = text.Trim();
        return text.Length <= maxChars ? text : text[^maxChars..];
    }
}
