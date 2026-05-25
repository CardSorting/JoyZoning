using JoyZoning.Jsdp;
using JoyZoning.Jsdp.Models;
using JoyZoning.Jsdp.Services;

namespace JoyZoning.Cli;

public static class JsdpHorizonCommand
{
    public static int Dispatch(CliContext ctx, string[] a, JSDPHarness harness)
    {
        RequireArgs(a, 3, "jsdp horizon <export|prompt|validate|diff|import|status>");

        var sub = a[2].ToLowerInvariant();

        return sub switch
        {
            "export" => WriteExport(ctx, harness, ParseRequestedNodes(ctx.Args)),
            "prompt" => WritePrompt(ctx, harness, ParseRequestedNodes(ctx.Args)),
            "validate" => WriteValidate(ctx, harness, a),
            "diff" => WriteDiff(ctx, harness, a),
            "import" => WriteImport(ctx, harness, a),
            "status" => CliOutput.WriteEnvelope(ctx, harness.HorizonStatus()),
            _ => throw new CliUsageException(
                "jsdp horizon export | prompt | validate | diff | import <horizon.json> | status"),
        };
    }

    private static int WriteExport(CliContext ctx, JSDPHarness harness, int nodes)
    {
        JsdpPlanningMode? mode = ctx.Args.Has("--mode") ? ParsePlanningMode(ctx.Args) : null;
        var result = harness.HorizonExport(nodes, mode);
        if (!ctx.Quiet)
        {
            Console.Error.WriteLine($"Horizon context: {result.HorizonContextPath} ({result.ContextByteSize} bytes)");
            foreach (var w in result.Warnings)
                Console.Error.WriteLine($"Warning: {w}");
        }
        return CliOutput.WriteEnvelope(ctx, result);
    }

    private static int WritePrompt(CliContext ctx, JSDPHarness harness, int nodes)
    {
        JsdpPlanningMode? mode = ctx.Args.Has("--mode") ? ParsePlanningMode(ctx.Args) : null;
        var result = harness.HorizonPrompt(nodes, mode);
        if (!ctx.Quiet)
            Console.Error.WriteLine($"Horizon prompt: {result.PromptPath}");
        return CliOutput.WriteEnvelope(ctx, result);
    }

    private static int WriteValidate(CliContext ctx, JSDPHarness harness, string[] a)
    {
        var path = ResolveProposalPath(a, ctx.Args);
        int? nodes = ctx.Args.Has("--nodes") ? ParseRequestedNodes(ctx.Args) : null;
        var result = harness.HorizonValidate(path, nodes);
        var code = CliOutput.WriteEnvelope(ctx, result);
        return result.Valid ? code : code == 0 ? 1 : code;
    }

    private static int WriteDiff(CliContext ctx, JSDPHarness harness, string[] a)
    {
        var path = ResolveProposalPath(a, ctx.Args);
        int? nodes = ctx.Args.Has("--nodes") ? ParseRequestedNodes(ctx.Args) : null;
        var result = harness.HorizonDiff(path, nodes);
        var code = CliOutput.WriteEnvelope(ctx, result);
        return result.PlanValid ? code : code == 0 ? 1 : code;
    }

    private static int WriteImport(CliContext ctx, JSDPHarness harness, string[] a)
    {
        var path = ResolveProposalPath(a, ctx.Args);
        var dryRun = ctx.Args.Has("--dry-run");
        var force = ctx.Args.Has("--force");
        var result = harness.HorizonImport(path, dryRun, force);
        if (!ctx.Quiet && result.ReportPath is { Length: > 0 } report)
            Console.Error.WriteLine($"Import report: {report}");
        var code = CliOutput.WriteEnvelope(ctx, result);
        if (dryRun)
            return result.Validation.Valid ? code : 1;
        return result.Imported ? code : 1;
    }

    private static int ParseRequestedNodes(CliArgs args)
    {
        var raw = args.Opt("--nodes") ?? JsdpContract.DefaultHorizonNodes.ToString();
        if (!int.TryParse(raw, out var n))
            throw new CliUsageException($"Invalid --nodes value: {raw}");
        return JsdpHorizonService.ClampRequestedNodes(n);
    }

    private static JsdpPlanningMode ParsePlanningMode(CliArgs args)
    {
        var mode = args.Opt("--mode") ?? "vertical-slices";
        return mode.ToLowerInvariant() switch
        {
            "vertical-slices" or "vertical" => JsdpPlanningMode.VerticalSlices,
            "systems-first" or "systems" => JsdpPlanningMode.SystemsFirst,
            "risk-first" or "risk" => JsdpPlanningMode.RiskFirst,
            _ => throw new CliUsageException($"Unknown planning mode: {mode}"),
        };
    }

    private static string ResolveProposalPath(string[] a, CliArgs args)
    {
        var path = args.Opt("--file") ?? (a.Length > 3 ? a[3] : null);
        if (string.IsNullOrWhiteSpace(path))
            throw new CliUsageException(
                "jsdp horizon validate|diff|import <horizon.json> [--file path]");
        return path;
    }

    private static void RequireArgs(string[] a, int min, string usage)
    {
        if (a.Length < min)
            throw new CliUsageException(usage);
    }
}
