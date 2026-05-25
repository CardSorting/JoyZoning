using JoyZoning.Jsdp;
using JoyZoning.Jsdp.Models;
using JoyZoning.Jsdp.Services;
using Xunit;

namespace JoyZoning.Cli.Tests;

public sealed class JsdpHarnessTests : IDisposable
{
    private readonly string _root;

    public JsdpHarnessTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "jsdp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Inspect_returns_spec_dag_and_lineage()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Inspect", null);
        harness.Analyze();
        harness.Plan(JsdpPlanningMode.VerticalSlices);
        var report = harness.Inspect();
        Assert.NotNull(report.SpecAnalysis);
        Assert.NotNull(report.Dag);
        Assert.NotNull(report.Config);
    }

    [Fact]
    public void Doctor_reports_ok_for_valid_run()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Doctor", null);
        harness.Analyze();
        harness.Plan(JsdpPlanningMode.VerticalSlices);
        var report = harness.Doctor();
        Assert.True(report.Checks.Count > 0);
        Assert.Contains(report.Checks, c => c.Id == "run_parse" && c.Status == "ok");
    }

    [Fact]
    public void Init_creates_config_json()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("Config test", null);
        Assert.True(File.Exists(JoyZoning.Jsdp.JsdpPaths.Config(_root)));
    }

    [Fact]
    public void Analyze_enriches_from_repo_scan()
    {
        File.WriteAllText(Path.Combine(_root, "JoyZoning.sln"), "");
        var harness = new JSDPHarness(_root);
        harness.Init("Scan", null);
        var analysis = harness.Analyze();
        var spec = new JSDPStateStore(_root).LoadProjectSpec();
        Assert.NotNull(spec.RepoScan);
        Assert.Contains("JoyZoning.sln", spec.RepoScan!.SolutionFiles);
    }

    [Fact]
    public void Init_creates_jsdp_directory()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("Build JoyMon overworld");

        Assert.True(Directory.Exists(JsdpPaths.Root(_root)));
        Assert.True(File.Exists(JsdpPaths.Run(_root)));
        Assert.True(File.Exists(JsdpPaths.ProjectSpec(_root)));
    }

    [Fact]
    public void Analyze_extracts_project_metadata()
    {
        var specPath = Path.Combine(_root, "PROJECT_SPEC.md");
        File.WriteAllText(specPath, """
            # JoyMon Tile RPG

            ## Tech stack
            - MonoGame
            - .NET 8

            ## Core systems
            - Overworld movement
            - Encounter zones

            ## Constraints
            - Deterministic combat

            ## Acceptance criteria
            - Player can walk on tilemap

            ## Verification
            - dotnet build
            - dotnet test
            """);

        var harness = new JSDPHarness(_root);
        harness.Init("", specPath);
        var analysis = harness.Analyze();

        Assert.Contains("JoyMon", analysis.ProductGoal, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(analysis.TechStack, s => s.Contains("MonoGame", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(analysis.CoreSystems, s => s.Contains("Overworld", StringComparison.OrdinalIgnoreCase));
        Assert.True(analysis.VerificationStrategy.Count > 0);
    }

    [Fact]
    public void Plan_creates_valid_project_specific_DAG()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("JoyMon encounter loop", null);
        harness.Analyze();
        var run = harness.Plan(JsdpPlanningMode.VerticalSlices);

        Assert.True(run.Nodes.Count >= 5);
        Assert.Contains(run.Nodes.Values, n => n.Title.Contains("JoyMon", StringComparison.OrdinalIgnoreCase)
            || n.Intent.Contains("encounter", StringComparison.OrdinalIgnoreCase)
            || n.Title.Contains("overworld", StringComparison.OrdinalIgnoreCase)
            || n.Intent.Contains("Overworld", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Nodes_preserve_dependency_ordering()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Ordering test", null);
        harness.Analyze();
        var run = harness.Plan(JsdpPlanningMode.SystemsFirst);

        var ordered = PromptDAGBuilder.TopologicalOrder(run.Nodes.Values).Select(n => n.Id).ToList();
        foreach (var node in run.Nodes.Values)
        {
            var depIndexes = node.Dependencies.Select(d => ordered.IndexOf(d)).ToList();
            var selfIndex = ordered.IndexOf(node.Id);
            Assert.All(depIndexes, idx => Assert.True(idx < selfIndex));
        }
    }

    [Fact]
    public void Next_skips_blocked_nodes()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Skip blocked", null);
        harness.Analyze();
        var run = harness.Plan(JsdpPlanningMode.VerticalSlices);
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        run.Nodes["002"].Status = JsdpNodeStatus.Blocked;
        new JSDPStateStore(_root).SaveRun(run);

        var next = harness.Next();
        Assert.False(next.Ready);
        Assert.DoesNotContain("002", next.NodeId ?? "");
    }

    [Fact]
    public void Verify_marks_pass_and_fail_correctly()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Verify states", null);
        harness.Analyze();
        var run = harness.Plan(JsdpPlanningMode.RiskFirst);
        run.Nodes.Values.First().VerificationCommands = ["echo \"ok\""];
        new JSDPStateStore(_root).SaveRun(run);

        var next = harness.Next();
        var pass = harness.Verify(next.NodeId);
        Assert.True(pass.Passed);

        run = new JSDPStateStore(_root).LoadRun();
        run.Nodes.Values.First(n => n.Id == next.NodeId).VerificationCommands = ["false"];
        new JSDPStateStore(_root).SaveRun(run);
        var fail = harness.Verify(next.NodeId);
        Assert.False(fail.Passed);
    }

    [Fact]
    public void Continue_creates_repair_nodes_on_failure()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Repair flow", null);
        harness.Analyze();
        var run = harness.Plan(JsdpPlanningMode.VerticalSlices);
        var first = run.Nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal).First();
        first.Status = JsdpNodeStatus.Failed;
        run.CurrentNodeId = first.Id;
        new JSDPStateStore(_root).SaveRun(run);

        var result = harness.Continue();
        Assert.Equal(JsdpContinuationAction.RepairCreated, result.Action);
        var updated = new JSDPStateStore(_root).LoadRun();
        Assert.Contains(updated.Nodes.Keys, k => k.Contains('R', StringComparison.Ordinal));
    }

    [Fact]
    public void Continue_advances_after_success()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Advance flow", null);
        harness.Analyze();
        var run = harness.Plan(JsdpPlanningMode.VerticalSlices);
        var first = run.Nodes.Values.OrderBy(n => n.Id, StringComparer.Ordinal).First();
        first.Status = JsdpNodeStatus.Verified;
        run.CurrentNodeId = first.Id;
        new JSDPStateStore(_root).SaveRun(run);

        var result = harness.Continue();
        Assert.Equal(JsdpContinuationAction.Advanced, result.Action);
        Assert.NotNull(result.NodeId);
        Assert.NotEqual(first.Id, result.NodeId);
    }

    [Fact]
    public void Ledger_remains_append_only()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Ledger", null);
        harness.Analyze();
        harness.Plan(JsdpPlanningMode.VerticalSlices);
        harness.Next();
        harness.Verify();

        var ledger = new JSDPLedger(_root);
        var count1 = ledger.EntryCount();
        harness.Record("001", "manual record");
        var count2 = ledger.EntryCount();
        Assert.Equal(count1 + 1, count2);

        var lines = File.ReadAllLines(JsdpPaths.Ledger(_root)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        Assert.True(lines.Count >= 2);
        Assert.Equal(lines.Count, count2);
    }

    [Fact]
    public void State_survives_restart()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Restart", null);
        harness.Analyze();
        var run = harness.Plan(JsdpPlanningMode.SystemsFirst);

        var reloaded = new JSDPHarness(_root).Status();
        Assert.Equal(run.Id, reloaded.Run.Id);
        Assert.Equal(run.Nodes.Count, reloaded.Run.Nodes.Count);
    }

    [Fact]
    public void Planning_modes_produce_distinct_DAG_shapes()
    {
        SeedSpec();
        var harness = new JSDPHarness(_root);
        harness.Init("Modes", null);
        harness.Analyze();

        var vertical = harness.Plan(JsdpPlanningMode.VerticalSlices);
        var systems = harness.Plan(JsdpPlanningMode.SystemsFirst);
        var risk = harness.Plan(JsdpPlanningMode.RiskFirst);

        Assert.NotEqual(
            string.Join("|", vertical.Nodes.Values.Select(n => n.Title)),
            string.Join("|", systems.Nodes.Values.Select(n => n.Title)));
        Assert.Contains(risk.Nodes.Values, n => n.Title.Contains("constraint", StringComparison.OrdinalIgnoreCase)
            || n.Title.Contains("parallel", StringComparison.OrdinalIgnoreCase)
            || n.Title.Contains("replay", StringComparison.OrdinalIgnoreCase)
            || n.Title.Contains("risk", StringComparison.OrdinalIgnoreCase)
            || n.Intent.Contains("replay", StringComparison.OrdinalIgnoreCase));
    }

    private void SeedSpec()
    {
        File.WriteAllText(Path.Combine(_root, "PROJECT_SPEC.md"), """
            # JoyMon

            ## Tech stack
            - MonoGame

            ## Core systems
            - Overworld movement
            - Encounter zones

            ## Verification
            - echo "ok"
            """);
    }
}
