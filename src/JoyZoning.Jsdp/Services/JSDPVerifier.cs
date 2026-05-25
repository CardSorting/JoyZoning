using System.Diagnostics;
using System.Text;
using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPVerifier
{
    private readonly string _workspaceRoot;

    public JSDPVerifier(string workspaceRoot) => _workspaceRoot = workspaceRoot;

    public VerificationReport VerifyNode(JsdpNode node)
    {
        var results = new List<VerificationCommandResult>();
        var failures = new List<string>();

        foreach (var command in node.VerificationCommands)
        {
            var result = RunCommand(command);
            results.Add(result);
            if (!result.Passed)
                failures.Add($"{command} (exit {result.ExitCode})");
        }

        var passed = failures.Count == 0;
        return new VerificationReport
        {
            NodeId = node.Id,
            Passed = passed,
            Results = results,
            Failures = failures,
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    public string WriteReportMarkdown(VerificationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Verification Report: {report.NodeId}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {report.GeneratedAt}");
        sb.AppendLine($"**Result:** {(report.Passed ? "PASSED" : "FAILED")}");
        sb.AppendLine();

        foreach (var r in report.Results)
        {
            sb.AppendLine($"## `{r.Command}`");
            sb.AppendLine();
            sb.AppendLine($"- Exit code: {r.ExitCode}");
            sb.AppendLine($"- Status: {(r.Passed ? "pass" : "fail")}");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(TrimOutput(r.Output, 4000));
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (report.Failures.Count > 0)
        {
            sb.AppendLine("## Failures");
            foreach (var f in report.Failures)
                sb.AppendLine($"- {f}");
        }

        var path = JsdpPaths.VerificationReportFile(_workspaceRoot, report.NodeId);
        Directory.CreateDirectory(JsdpPaths.Reports(_workspaceRoot));
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private VerificationCommandResult RunCommand(string command)
    {
        if (command.StartsWith("echo ", StringComparison.Ordinal))
        {
            return new VerificationCommandResult
            {
                Command = command,
                ExitCode = 0,
                Passed = true,
                Output = command["echo ".Length..].Trim('"'),
            };
        }

        var (exitCode, stdout, stderr) = ShellRunner.Run(_workspaceRoot, command);
        var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + "\n" + stderr;
        return new VerificationCommandResult
        {
            Command = command,
            ExitCode = exitCode,
            Passed = exitCode == 0,
            Output = output,
        };
    }

    private static string TrimOutput(string output, int max) =>
        output.Length <= max ? output : output[..max] + "\n... (truncated)";
}
