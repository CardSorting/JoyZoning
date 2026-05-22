namespace JoyZoning.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"] or ["help"])
        {
            CliOutput.PrintHelpEmbedded();
            return 0;
        }

        var ctx = CliContext.FromArgs(args);
        try
        {
            using var client = new JoyZoningCliClient(ctx.BaseUrl);
            return await CliDispatcher.DispatchAsync(client, ctx);
        }
        catch (CliUsageException ex)
        {
            if (!ctx.Quiet)
                CliOutput.WriteUsageError(ex.Message);
            else
                Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }
}
