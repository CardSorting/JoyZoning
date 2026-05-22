using System.Text.Json;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class YoloVerificationCommands
{
    public static IReadOnlyList<string> Resolve(YoloPolicy policy, JsonElement? lease)
    {
        var commands = new List<string>();
        foreach (var cmd in policy.RequiredVerificationCommands)
        {
            policy.AssertCommandAllowed(cmd);
            if (!commands.Contains(cmd, StringComparer.Ordinal))
                commands.Add(cmd);
        }

        if (!policy.MergeHandoffVerificationCommands || lease is null)
            return commands;

        var handoff = YoloLeaseJson.TryReadHandoff(lease.Value);
        if (handoff is null)
            return commands;

        foreach (var cmd in handoff.VerificationCommands)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                continue;
            policy.AssertCommandAllowed(cmd);
            if (!commands.Contains(cmd, StringComparer.Ordinal))
                commands.Add(cmd);
        }

        return commands;
    }
}
