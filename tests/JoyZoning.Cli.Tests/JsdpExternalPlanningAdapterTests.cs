using JoyZoning.Jsdp;
using JoyZoning.Jsdp.Models;
using JoyZoning.Jsdp.Services;
using Xunit;

namespace JoyZoning.Cli.Tests;

public sealed class JsdpExternalPlanningAdapterTests : IDisposable
{
    private readonly string _root;
    private readonly string _fixture;

    public JsdpExternalPlanningAdapterTests()
    {
        _fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "jsdp");
        _root = Path.Combine(Path.GetTempPath(), "jsdp-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.Copy(Path.Combine(_fixture, "PROJECT_SPEC.md"), Path.Combine(_root, "PROJECT_SPEC.md"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ExportPlanningContext_writes_state_file()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("", Path.Combine(_root, "PROJECT_SPEC.md"));
        harness.Analyze();

        var export = harness.ExportPlanningContext(JsdpPlanningMode.VerticalSlices);

        Assert.True(File.Exists(export.Path));
        Assert.Equal(JsdpPaths.PlanningContext(_root), export.Path);
        Assert.NotNull(export.Context.SpecAnalysis);
    }

    [Fact]
    public void ValidatePlan_rejects_vague_nodes()
    {
        var harness = new JSDPHarness(_root);
        var result = harness.ValidatePlan(Path.Combine(_fixture, "vague-plan.json"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("verificationCommands", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePlan_accepts_valid_plan()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        var result = harness.ValidatePlan(Path.Combine(_fixture, "valid-plan.json"));
        Assert.True(result.Valid);
        Assert.Equal(2, result.NodeCount);
    }

    [Fact]
    public void ImportPlan_writes_run_and_tree_when_valid()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        var import = harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        Assert.True(import.Imported);
        Assert.True(File.Exists(JsdpPaths.Run(_root)));
        Assert.True(File.Exists(JsdpPaths.Tree(_root)));
        var run = new JSDPStateStore(_root).LoadRun();
        Assert.Equal(2, run.Nodes.Count);
    }

    [Fact]
    public void ImportPlan_does_not_write_when_invalid()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        var before = new JSDPStateStore(_root).LoadRun().Nodes.Count;

        var import = harness.ImportPlan(Path.Combine(_fixture, "vague-plan.json"));

        Assert.False(import.Imported);
        Assert.Equal(before, new JSDPStateStore(_root).LoadRun().Nodes.Count);
    }

    [Fact]
    public void PlanningPrompt_writes_prompt_and_schema_paths()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        var result = harness.PlanningPrompt(JsdpPlanningMode.VerticalSlices);

        Assert.True(File.Exists(result.PromptPath));
        Assert.True(File.Exists(result.PlanningContextPath));
        Assert.True(File.Exists(result.SchemaPath));
        Assert.Contains("plan.json", File.ReadAllText(result.PromptPath));
    }

    [Fact]
    public void ExportPlanningContext_auto_analyzes_when_missing()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("", Path.Combine(_root, "PROJECT_SPEC.md"));

        var export = harness.ExportPlanningContext(JsdpPlanningMode.VerticalSlices);

        Assert.NotNull(export.Context.SpecAnalysis);
    }

    [Fact]
    public void ExportPlanningContext_includes_contract_version()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        var export = harness.ExportPlanningContext(JsdpPlanningMode.VerticalSlices);

        Assert.Equal(JsdpContract.PlanningContextVersion, export.Context.ContractVersion);
    }

    [Fact]
    public void ValidatePlan_rejects_cycle()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        var result = harness.ValidatePlan(Path.Combine(_fixture, "cycle-plan.json"));
        Assert.False(result.Valid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidatePlan_rejects_self_dependency()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        var result = harness.ValidatePlan(Path.Combine(_fixture, "self-dep-plan.json"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("itself", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePlan_rejects_protected_mutation_surface()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        var result = harness.ValidatePlan(Path.Combine(_fixture, "protected-surface-plan.json"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains(".jsdp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImportPlan_dry_run_does_not_write()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.Plan(JsdpPlanningMode.VerticalSlices);
        var before = new JSDPStateStore(_root).LoadRun().Nodes.Count;

        var import = harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"), dryRun: true);

        Assert.False(import.Imported);
        Assert.True(import.DryRun);
        Assert.True(import.Validation.Valid);
        Assert.Equal(2, import.NodeCount);
        Assert.Equal(before, new JSDPStateStore(_root).LoadRun().Nodes.Count);
    }

    [Fact]
    public void ImportPlan_appends_ledger_on_success()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var ledger = new JSDPLedger(_root).ReadAll();
        Assert.Contains(ledger, e => e.NodeId == "plan-import");
    }

    [Fact]
    public void Doctor_reports_external_planning_readiness()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ExportPlanningContext(JsdpPlanningMode.VerticalSlices);

        var report = harness.Doctor();

        Assert.Contains(report.Checks, c => c.Id == "planning_context" && c.Status == "ok");
    }

    [Fact]
    public void ValidatePlan_rejects_unsupported_contract_version()
    {
        var harness = new JSDPHarness(_root);
        var result = harness.ValidatePlan(Path.Combine(_fixture, "bad-contract-plan.json"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("contractVersion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePlan_rejects_more_than_max_nodes()
    {
        var nodes = Enumerable.Range(1, JsdpContract.MaxPlanNodes + 1).Select(i => new ExternalJsdpPlanNode
        {
            Id = i.ToString("D3"),
            Title = $"Bootstrap MonoGame slice {i} for MiniApp",
            Intent = $"Implement concrete MiniGame overworld feature slice {i} with tests.",
            VerificationCommands = ["dotnet build MiniApp.sln"],
            AllowedMutationSurface = ["src/"],
        }).ToList();

        var result = new JsdpExternalPlanValidator().Validate(new ExternalJsdpPlanDocument { Nodes = nodes });
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("maximum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePlan_warns_dangerous_verification_commands()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        var planPath = Path.Combine(_root, "danger-plan.json");
        File.WriteAllText(planPath, """
            {
              "nodes": [{
                "id": "001",
                "title": "Bootstrap MonoGame overworld shell for MiniApp",
                "intent": "Establish minimal MonoGame runtime with tilemap load and scene bootstrap for MiniApp overworld.",
                "verificationCommands": ["rm -rf /"],
                "allowedMutationSurface": ["src/"]
              }]
            }
            """);

        var result = harness.ValidatePlan(planPath);
        Assert.Contains(result.Warnings, w => w.Contains("destructive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DiffPlan_reports_node_delta()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var diff = harness.DiffPlan(Path.Combine(_fixture, "valid-plan.json"));

        Assert.True(diff.PlanValid);
        Assert.Equal(2, diff.InBoth.Count);
        Assert.Empty(diff.OnlyInPlan);
        Assert.Empty(diff.OnlyInRun);
    }

    [Fact]
    public void ImportPlan_blocks_when_verified_nodes_exist_without_force()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        var blocked = harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));
        Assert.False(blocked.Imported);
        Assert.Contains(blocked.Validation.Errors, e => e.Contains("verified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ImportPlan_allows_replace_when_force()
    {
        var harness = new JSDPHarness(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        var import = harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"), force: true);
        Assert.True(import.Imported);
    }
}
