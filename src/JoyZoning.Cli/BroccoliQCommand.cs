namespace JoyZoning.Cli;

public static class BroccoliQCommand
{
    public static async Task<int> DispatchAsync(JoyZoningCliClient client, CliContext ctx, string[] args)
    {
        if (args.Length < 2)
            throw new CliUsageException("Usage: jz broccoliq <status|backfill|flush|audit|tasks>");

        var sub = args[1].ToLowerInvariant();
        return sub switch
        {
            "status" or "health" => CliOutput.WriteResult(ctx, await client.BroccoliQHealthAsync()),
            "backfill" => await RunBackfillAsync(client, ctx, args),
            "flush" => CliOutput.WriteResult(ctx, await client.BroccoliQFlushAsync()),
            "audit" => CliOutput.WriteResult(ctx, await client.BroccoliQAuditAsync(ParseLimit(args, 2, 50))),
            "tasks" => CliOutput.WriteResult(ctx, await client.BroccoliQTasksAsync(ParseLimit(args, 2, 50), ParseTaskId(args))),
            _ => throw new CliUsageException($"Unknown broccoliq subcommand: {sub}"),
        };
    }

    private static async Task<int> RunBackfillAsync(JoyZoningCliClient client, CliContext ctx, string[] args)
    {
        int? max = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--max" && i + 1 < args.Length && int.TryParse(args[i + 1], out var m))
                max = m;
        }

        return CliOutput.WriteResult(ctx, await client.BroccoliQBackfillAsync(max));
    }

    private static int ParseLimit(string[] args, int flagIndex, int defaultValue)
    {
        if (flagIndex + 1 < args.Length && int.TryParse(args[flagIndex + 1], out var n))
            return Math.Clamp(n, 1, 500);
        return defaultValue;
    }

    private static Guid? ParseTaskId(string[] args)
    {
        for (var i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "--task" && Guid.TryParse(args[i + 1], out var id))
                return id;
        }

        return null;
    }
}
