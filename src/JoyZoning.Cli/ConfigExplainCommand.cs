namespace JoyZoning.Cli;

public static class ConfigExplainCommand
{
    public static int Run(CliContext ctx)
    {
        var lines = new Dictionary<string, object>
        {
            ["ok"] = true,
            ["environment"] = new object[]
            {
                new Dictionary<string, string>
                {
                    ["name"] = CliContext.BaseUrlEnv,
                    ["description"] = "Control plane base URL",
                    ["default"] = CliContext.DefaultBaseUrl,
                },
                new Dictionary<string, string>
                {
                    ["name"] = CliContext.SessionEnv,
                    ["description"] = "Default operator session GUID for task commands",
                },
                new Dictionary<string, string>
                {
                    ["name"] = CliArgs.TaskIdEnv,
                    ["description"] = "Default kanban task/card GUID",
                },
            },
            ["authority"] = new[]
            {
                "DietCode cannot mark tasks Complete; use jz task complete (merge API).",
                "Critical dispatch requires --approve-critical on every dispatch/retry.",
                "merge, revoke, and recover replace require --yes.",
                "Revoked and merged leases are terminal.",
                "Recovery preserves evidence; replacement supersedes prior lease.",
            },
            ["exitCodes"] = new Dictionary<string, int>
            {
                ["success"] = 0,
                ["apiOrRuntime"] = 1,
                ["usageNetworkConfig"] = 2,
            },
        };

        return CliOutput.WriteEnvelope(ctx, lines);
    }
}
