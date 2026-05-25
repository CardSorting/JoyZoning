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
}
