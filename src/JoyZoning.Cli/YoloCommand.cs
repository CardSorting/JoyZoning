using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class YoloCommand
{
    public static async Task<int> DispatchAsync(
        JoyZoningCliClient client,
        CliContext ctx,
        string[] positionals)
    {
        RequireArgs(positionals, 2, "yolo <plan|run|stop|status>");
        var sub = positionals[1].ToLowerInvariant();
        var yoloClient = new YoloRunClientAdapter(client);

        return sub switch
        {
            "plan" => await PlanAsync(ctx, yoloClient),
            "run" => await RunAsync(ctx, yoloClient),
            "stop" => CliOutput.WriteEnvelope(ctx, YoloRunner.Stop()),
            "status" => CliOutput.WriteEnvelope(ctx, YoloRunner.Status()),
            _ => throw new CliUsageException("yolo plan | run | stop | status"),
        };
    }

    private static async Task<int> PlanAsync(CliContext ctx, IYoloRunClient client)
    {
        var policyPath = RequirePolicyPath(ctx.Args);
        var policy = YoloPolicy.LoadFromFile(policyPath);
        var envelope = await YoloRunner.PlanAsync(client, policy);
        return CliOutput.WriteEnvelope(ctx, envelope);
    }

    private static async Task<int> RunAsync(CliContext ctx, IYoloRunClient client)
    {
        CliSafety.RequireYes(ctx.Args, "jz yolo run (autonomous supervised execution)");
        var policyPath = RequirePolicyPath(ctx.Args);
        var policy = YoloPolicy.LoadFromFile(policyPath);

        if (ctx.Args.SessionId.HasValue && ctx.Args.SessionId != policy.SessionId)
            throw new CliUsageException("--session must match policy sessionId when both are set.");

        try
        {
            var envelope = await YoloRunner.RunAsync(client, ctx.Args, policy);
            return CliOutput.WriteEnvelope(ctx, envelope);
        }
        catch (YoloPolicyViolationException ex)
        {
            CliOutput.WriteUsageError(ex.Message);
            return 1;
        }
    }

    private static string RequirePolicyPath(CliArgs args)
    {
        var path = args.Opt("--policy");
        if (string.IsNullOrWhiteSpace(path))
            throw new CliUsageException("yolo requires --policy <path> (e.g. .joyzoning/yolo.policy.json).");
        return Path.GetFullPath(path);
    }

    private static void RequireArgs(string[] a, int min, string hint)
    {
        if (a.Length < min)
            throw new CliUsageException(hint);
    }
}
