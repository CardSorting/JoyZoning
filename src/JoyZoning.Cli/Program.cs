using JoyZoning.Cli.Tui;
using JoyZoning.Jsdp.Services;

namespace JoyZoning.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is ["--help"] or ["-h"] or ["help"])
        {
            CliOutput.PrintHelpEmbedded();
            return 0;
        }

        if (OperatorTuiRunner.ShouldLaunchInteractive(args))
            return await OperatorTuiRunner.RunAsync(CliContext.FromArgs([]));

        var ctx = CliContext.FromArgs(args);
        try
        {
            if (OperatorTuiRunner.WantsTui(ctx.Args) &&
                (ctx.Args.Positionals.Length == 0 ||
                 ctx.Args.Positionals[0].Equals("tui", StringComparison.OrdinalIgnoreCase)))
            {
                return await OperatorTuiRunner.RunAsync(ctx);
            }

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
        catch (JsdpException ex)
        {
            if (!ctx.Quiet)
                CliOutput.WriteUsageError(ex.Message);
            else
                Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
