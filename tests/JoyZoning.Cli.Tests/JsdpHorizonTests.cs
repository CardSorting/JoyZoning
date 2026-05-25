using System.Text.Json;
using JoyZoning.Jsdp;
using JoyZoning.Jsdp.Models;
using JoyZoning.Jsdp.Services;
using Xunit;

namespace JoyZoning.Cli.Tests;

public sealed class JsdpHorizonTests : IDisposable
{
    private readonly string _root;
    private readonly string _fixture;

    public JsdpHorizonTests()
    {
        _fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "jsdp");
        _root = Path.Combine(Path.GetTempPath(), "jsdp-horizon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.Copy(Path.Combine(_fixture, "PROJECT_SPEC.md"), Path.Combine(_root, "PROJECT_SPEC.md"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void HorizonExport_excludes_full_ledger_and_spec_markdown()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("", Path.Combine(_root, "PROJECT_SPEC.md"));
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        for (var i = 0; i < 12; i++)
            harness.Record("001", $"summary line {i}");

        var export = harness.HorizonExport(3);

        var ctxJson = File.ReadAllText(export.HorizonContextPath);
        Assert.DoesNotContain("summary line 0", ctxJson);
        Assert.Contains("summary line 11", ctxJson);
        Assert.True(export.Context.RecentLedgerSummaries.Count <= JsdpContract.MaxHorizonLedgerSummaries);

        var spec = harness.Analyze();
        var raw = File.ReadAllText(Path.Combine(_root, "PROJECT_SPEC.md"));
        if (raw.Length > 40)
            Assert.DoesNotContain(raw[..40], ctxJson);

        Assert.DoesNotContain("ledger.jsonl", ctxJson);
        Assert.DoesNotContain("rawMarkdown", ctxJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("project-spec.json", ctxJson);
    }

    [Fact]
    public void HorizonPrompt_contains_bounded_planning_rules()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        var result = harness.HorizonPrompt(3);
        var text = File.ReadAllText(result.PromptPath);

        Assert.Contains("not", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("whole project", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("at most 3", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON only", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HorizonValidate_rejects_oversized_proposal()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.HorizonExport(3);

        var result = harness.HorizonValidate(Path.Combine(_fixture, "oversized-horizon.json"));

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("at most 3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HorizonValidate_rejects_vague_nodes()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.HorizonExport(5);

        var result = harness.HorizonValidate(Path.Combine(_fixture, "vague-horizon.json"));

        Assert.False(result.Valid);
    }

    [Fact]
    public void HorizonImport_appends_without_overwriting_verified_nodes()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        run.Nodes["002"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        harness.HorizonExport(5);
        var import = harness.HorizonImport(Path.Combine(_fixture, "valid-horizon.json"));

        Assert.True(import.Imported);
        Assert.True(import.AppendedNodeCount >= 1);

        run = store.LoadRun();
        Assert.Equal(JsdpNodeStatus.Verified, run.Nodes["001"].Status);
        Assert.True(run.Nodes.Count > 2);
        Assert.True(File.Exists(import.ReportPath));
    }

    [Fact]
    public void HorizonValidate_rejects_unknown_dependencies()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.HorizonExport(3);

        var result = harness.HorizonValidate(Path.Combine(_fixture, "unknown-dep-horizon.json"));

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HorizonStatus_summarizes_frontier()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var status = harness.HorizonStatus();

        Assert.Equal(2, status.DagSize);
        Assert.True(status.SuggestedNextHorizonSize >= JsdpContract.MinHorizonNodes);
        Assert.NotNull(status.Frontier);
    }

    [Fact]
    public void HorizonExport_writes_state_artifacts()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        harness.HorizonExport(4);

        Assert.True(File.Exists(JsdpPaths.HorizonContext(_root)));
        Assert.True(File.Exists(JsdpPaths.ProjectSummary(_root)));
        Assert.True(File.Exists(JsdpPaths.RepoSummary(_root)));
        Assert.True(File.Exists(JsdpPaths.Frontier(_root)));
    }

    [Fact]
    public void HorizonImport_blocks_when_failed_nodes_without_force()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["002"].Status = JsdpNodeStatus.Failed;
        store.SaveRun(run);

        harness.HorizonExport(5);
        var import = harness.HorizonImport(Path.Combine(_fixture, "valid-horizon.json"));

        Assert.False(import.Imported);
        Assert.Contains(import.Validation.Errors, e => e.Contains("Active failures", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HorizonImport_dry_run_does_not_append()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["002"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        var before = store.LoadRun().Nodes.Count;
        harness.HorizonExport(5);
        var import = harness.HorizonImport(Path.Combine(_fixture, "valid-horizon.json"), dryRun: true);

        Assert.False(import.Imported);
        Assert.True(import.DryRun);
        Assert.True(import.Validation.Valid);
        Assert.Equal(before, store.LoadRun().Nodes.Count);
    }

    [Fact]
    public void HorizonValidate_uses_live_frontier_after_run_changes()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));
        harness.HorizonExport(5);

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        run.Nodes["002"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        var result = harness.HorizonValidate(Path.Combine(_fixture, "valid-horizon.json"));
        Assert.True(result.Valid);
    }

    [Fact]
    public void HorizonImport_dry_run_reports_projected_final_ids()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        run.Nodes["002"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        harness.HorizonExport(5);
        var import = harness.HorizonImport(Path.Combine(_fixture, "valid-horizon.json"), dryRun: true);

        Assert.True(import.Validation.Valid);
        Assert.Single(import.ProjectedNodeIds);
        Assert.Equal("003", import.ProjectedNodeIds[0]);
    }

    [Fact]
    public void HorizonValidate_rejects_run_id_mismatch()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.HorizonExport(3);

        var ctx = JsdpJson.ReadFile<JsdpHorizonContext>(JsdpPaths.HorizonContext(_root));
        ctx.RunId = "stale-run-id";
        JsdpJson.WriteFile(JsdpPaths.HorizonContext(_root), ctx);

        var result = harness.HorizonValidate(Path.Combine(_fixture, "valid-horizon.json"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("runId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HorizonImport_refreshes_context_snapshot()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        run.Nodes["002"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        harness.HorizonExport(5);
        var before = File.GetLastWriteTimeUtc(JsdpPaths.HorizonContext(_root));

        var import = harness.HorizonImport(Path.Combine(_fixture, "valid-horizon.json"));

        Assert.True(import.Imported);
        Assert.True(import.ContextRefreshed);
        Assert.True(File.GetLastWriteTimeUtc(JsdpPaths.HorizonContext(_root)) >= before);

        var ctx = JsdpJson.ReadFile<JsdpHorizonContext>(JsdpPaths.HorizonContext(_root));
        Assert.Equal(3, ctx.DagSizeAtExport);
    }

    [Fact]
    public void HorizonValidate_requires_prior_export()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        Assert.Throws<JsdpException>(() =>
            harness.HorizonValidate(Path.Combine(_fixture, "valid-horizon.json")));
    }

    [Fact]
    public void HorizonDiff_requires_prior_export()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        Assert.Throws<JsdpException>(() =>
            harness.HorizonDiff(Path.Combine(_fixture, "valid-horizon.json")));
    }

    [Fact]
    public void HorizonDiff_reports_projected_appends()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var store = new JSDPStateStore(_root);
        var run = store.LoadRun();
        run.Nodes["001"].Status = JsdpNodeStatus.Verified;
        run.Nodes["002"].Status = JsdpNodeStatus.Verified;
        store.SaveRun(run);

        harness.HorizonExport(5);
        var diff = harness.HorizonDiff(Path.Combine(_fixture, "valid-horizon.json"));

        Assert.True(diff.PlanValid);
        Assert.Single(diff.ProjectedAppends);
        Assert.Equal(3, diff.ProjectedDagSize);
        Assert.Equal("003", diff.ProjectedAppends[0].ProjectedId);
    }

    [Fact]
    public void HorizonValidate_rejects_title_matching_existing_dag_node()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));
        harness.HorizonExport(5);

        var result = harness.HorizonValidate(
            Path.Combine(_fixture, "existing-dag-title-horizon.json"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("duplicates existing DAG", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HorizonValidate_rejects_duplicate_titles()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));
        harness.HorizonExport(5);

        var result = harness.HorizonValidate(Path.Combine(_fixture, "duplicate-title-horizon.json"));
        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HorizonExport_includes_existing_node_summaries_not_full_nodes()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));

        var export = harness.HorizonExport(3);
        var json = File.ReadAllText(export.HorizonContextPath);

        Assert.Contains("existingNodeSummaries", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"prompt\"", json);
        Assert.DoesNotContain("verificationCommands", json);
    }

    [Fact]
    public void HorizonExport_reports_context_byte_size()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();

        var export = harness.HorizonExport(3);

        Assert.True(export.ContextByteSize > 0);
        Assert.True(export.ContextWithinBudget);
    }

    [Fact]
    public void HorizonImport_appends_ledger_without_truncating()
    {
        var harness = JSDPHarness.ForInit(_root);
        harness.Init("MiniApp", null);
        harness.Analyze();
        harness.ImportPlan(Path.Combine(_fixture, "valid-plan.json"));
        var before = new JSDPLedger(_root).ReadAll().Count;

        harness.HorizonExport(5);
        harness.HorizonImport(Path.Combine(_fixture, "valid-horizon.json"));

        var after = new JSDPLedger(_root).ReadAll();
        Assert.True(after.Count > before);
        Assert.Contains(after, e => e.NodeId == "horizon-import");
    }
}
