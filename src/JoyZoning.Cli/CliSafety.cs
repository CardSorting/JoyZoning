using JoyZoning.Domain.Enums;

namespace JoyZoning.Cli;

public static class CliSafety
{
    public static void RequireYes(CliArgs args, string actionDescription)
    {
        if (!args.Yes)
            throw new CliUsageException($"{actionDescription} requires --yes (human-supervised confirmation).");
    }

    public static void RequireCriticalApproval(CliArgs args, bool taskIsCritical)
    {
        if (taskIsCritical && !args.ApproveCritical)
            throw new CliUsageException(
                "Critical task requires --approve-critical (explicit human approval).");
    }

    public static void ForbidDirectComplete(WorkTaskStatus status)
    {
        if (status == WorkTaskStatus.Complete)
            throw new CliUsageException(
                "Cannot set task status to Complete directly. Use: jz task complete <taskId> --yes");
    }

    public static void GuardRawRequest(CliArgs args, string method, string path)
    {
        var m = method.ToUpperInvariant();
        var p = path.ToLowerInvariant();

        if (m == "DELETE")
            RequireYes(args, "raw DELETE");

        if (m == "POST" && (p.Contains("/lease/merge") || p.Contains("/lease/revoke")))
            RequireYes(args, $"raw POST {path}");

        if (m == "POST" && p.Contains("/lease/recover") && args.Opt("--body") is { } bodyFile)
        {
            if (bodyFile != "@stdin" && File.Exists(bodyFile))
            {
                var text = File.ReadAllText(bodyFile);
                if (text.Contains("\"mode\":2", StringComparison.Ordinal) ||
                    text.Contains("\"mode\": 2", StringComparison.Ordinal))
                    RequireYes(args, "raw recover replacement lease");
            }
        }
    }
}
