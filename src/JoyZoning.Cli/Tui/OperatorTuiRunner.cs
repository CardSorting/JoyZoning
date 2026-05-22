namespace JoyZoning.Cli.Tui;

public static class OperatorTuiRunner
{
    public const string NoTuiEnv = "JOYZONING_NO_TUI";

    public static bool ShouldLaunchInteractive(string[] argv)
    {
        if (argv.Length > 0)
            return false;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(NoTuiEnv)))
            return false;

        return !Console.IsInputRedirected && !Console.IsOutputRedirected;
    }

    public static bool WantsTui(CliArgs args) =>
        args.Has("--tui") || args.Has("-t");

    public static async Task<int> RunAsync(CliContext ctx, CancellationToken cancellationToken = default)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            CliOutput.WriteUsageError(
                "Operator TUI requires an interactive terminal. Use JSON commands or set JOYZONING_NO_TUI=1.");
            return 2;
        }

        using var client = new JoyZoningCliClient(ctx.BaseUrl);
        var repl = new OperatorRepl(client, ctx);
        return await repl.RunAsync(cancellationToken);
    }
}
