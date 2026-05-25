using JoyZoning.Jsdp;
using JoyZoning.Jsdp.Models;
using JoyZoning.Jsdp.Services;

namespace JoyZoning.Cli;

public static class JsdpCommand
{
    public static int DispatchAsync(CliContext ctx, string[] a)
    {
        RequireArgs(a, 2, "jsdp <init|analyze|plan|...|diff-plan|export-planning-context|import-plan|validate-plan|planning-prompt>");

        var sub = a[1].ToLowerInvariant();
        var harness = CreateHarness();

        return sub switch
        {
            "init" => RunInit(ctx, a),
            "analyze" => CliOutput.WriteEnvelope(ctx, harness.Analyze()),
            "plan" => CliOutput.WriteEnvelope(ctx, harness.Plan(RequirePlanningMode(ctx.Args))),
            "next" => WriteNext(ctx, harness),
            "verify" => WriteVerify(ctx, harness),
            "continue" => CliOutput.WriteEnvelope(ctx, harness.Continue()),
            "status" => CliOutput.WriteEnvelope(ctx, harness.Status()),
            "inspect" => CliOutput.WriteEnvelope(ctx, harness.Inspect()),
            "doctor" => WriteDoctor(ctx, harness),
            "record" => WriteRecord(ctx, harness),
            "export-planning-context" => WriteExportPlanningContext(ctx, harness, RequirePlanningMode(ctx.Args)),
            "import-plan" => WriteImportPlan(ctx, harness, a),
            "validate-plan" => WriteValidatePlan(ctx, harness, a),
            "diff-plan" => WriteDiffPlan(ctx, harness, a),
            "planning-prompt" => WritePlanningPrompt(ctx, harness, RequirePlanningMode(ctx.Args)),
            "horizon" => JsdpHorizonCommand.Dispatch(ctx, a, harness),
            _ => throw new CliUsageException(
                "jsdp init | analyze | plan | next | verify | continue | status | inspect | doctor | record | "
                + "export-planning-context | import-plan | validate-plan | diff-plan | planning-prompt | "
                + "horizon export|prompt|validate|diff|import|status"),
        };
    }

    private static JSDPHarness CreateHarness() => new(WorkspaceLocator.Find());

    private static int RunInit(CliContext ctx, string[] a)
    {
        var harness = JSDPHarness.ForInit();
        var specPath = ctx.Args.Opt("--spec");
        string? goal = null;

        if (specPath is null)
        {
            if (a.Length < 3)
                throw new CliUsageException("jsdp init \"<goal>\" or jsdp init --spec <file>");
            goal = string.Join(' ', a.Skip(2));
        }
        else if (a.Length > 2 && !a[2].StartsWith("-", StringComparison.Ordinal))
        {
            goal = string.Join(' ', a.Skip(2).Where(x => !x.StartsWith("-", StringComparison.Ordinal)));
        }

        var result = harness.Init(goal ?? "", specPath);
        return CliOutput.WriteEnvelope(ctx, result);
    }

    private static int WriteNext(CliContext ctx, JSDPHarness harness)
    {
        var result = harness.Next();
        if (!result.Ready)
        {
            if (!ctx.Quiet)
                Console.Error.WriteLine(result.Message);
            return CliOutput.WriteEnvelope(ctx, result);
        }

        if (!ctx.Quiet && result.PromptPath is not null)
            Console.Error.WriteLine($"Prompt written: {result.PromptPath}");

        return CliOutput.WriteEnvelope(ctx, result);
    }

    private static int WriteVerify(CliContext ctx, JSDPHarness harness)
    {
        var nodeId = ctx.Args.Opt("--node");
        var result = harness.Verify(nodeId);
        var code = CliOutput.WriteEnvelope(ctx, result);
        return result.Passed ? code : 1;
    }

    private static int WriteDoctor(CliContext ctx, JSDPHarness harness)
    {
        var report = harness.Doctor();
        var code = CliOutput.WriteEnvelope(ctx, report);
        return report.Ok ? code : 1;
    }

    private static int WriteExportPlanningContext(CliContext ctx, JSDPHarness harness, JsdpPlanningMode mode)
    {
        var result = harness.ExportPlanningContext(mode);
        if (!ctx.Quiet)
            Console.Error.WriteLine($"Planning context: {result.Path}");
        return CliOutput.WriteEnvelope(ctx, result);
    }

    private static int WriteValidatePlan(CliContext ctx, JSDPHarness harness, string[] a)
    {
        var path = ResolvePlanPath(a, ctx.Args);
        var result = harness.ValidatePlan(path);
        var code = CliOutput.WriteEnvelope(ctx, result);
        return result.Valid ? code : code == 0 ? 1 : code;
    }

    private static int WriteImportPlan(CliContext ctx, JSDPHarness harness, string[] a)
    {
        var path = ResolvePlanPath(a, ctx.Args);
        JsdpPlanningMode? mode = ctx.Args.Has("--mode") ? RequirePlanningMode(ctx.Args) : null;
        var dryRun = ctx.Args.Has("--dry-run");
        var force = ctx.Args.Has("--force");
        var result = harness.ImportPlan(path, mode, dryRun, force);
        var code = CliOutput.WriteEnvelope(ctx, result);
        if (dryRun)
            return result.Validation.Valid ? code : 1;
        return result.Imported ? code : 1;
    }

    private static int WriteDiffPlan(CliContext ctx, JSDPHarness harness, string[] a)
    {
        var path = ResolvePlanPath(a, ctx.Args);
        return CliOutput.WriteEnvelope(ctx, harness.DiffPlan(path));
    }

    private static int WritePlanningPrompt(CliContext ctx, JSDPHarness harness, JsdpPlanningMode mode)
    {
        var result = harness.PlanningPrompt(mode);
        if (!ctx.Quiet)
            Console.Error.WriteLine($"Planning prompt: {result.PromptPath}");
        return CliOutput.WriteEnvelope(ctx, result);
    }

    private static string ResolvePlanPath(string[] a, CliArgs args)
    {
        var path = args.Opt("--plan") ?? (a.Length > 2 ? a[2] : null);
        if (string.IsNullOrWhiteSpace(path))
            throw new CliUsageException("jsdp import-plan <plan.json> or jsdp validate-plan <plan.json>");
        return path;
    }

    private static int WriteRecord(CliContext ctx, JSDPHarness harness)
    {
        var nodeId = ctx.Args.Opt("--node")
            ?? throw new CliUsageException("jsdp record requires --node <id>");
        var summary = ctx.Args.Opt("--summary")
            ?? throw new CliUsageException("jsdp record requires --summary \"<text>\"");
        var entry = harness.Record(nodeId, summary);
        return CliOutput.WriteEnvelope(ctx, entry);
    }

    private static JsdpPlanningMode RequirePlanningMode(CliArgs args) => ParsePlanningMode(args);

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

    private static void RequireArgs(string[] a, int min, string usage)
    {
        if (a.Length < min)
            throw new CliUsageException(usage);
    }
}
