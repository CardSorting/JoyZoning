namespace JoyZoning.Cli;

public static class BroccoliQCommand
{
    public static async Task<int> DispatchAsync(JoyZoningCliClient client, CliContext ctx, string[] args)
    {
        if (args.Length < 2)
            throw new CliUsageException("Usage: jz broccoliq <status|backfill|flush>");

        var sub = args[1].ToLowerInvariant();
        return sub switch
        {
            "status" or "health" => CliOutput.WriteResult(ctx, await client.BroccoliQHealthAsync()),
            "backfill" => await RunBackfillAsync(client, ctx, args),
            "flush" => CliOutput.WriteResult(ctx, await client.BroccoliQFlushAsync()),
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
}
