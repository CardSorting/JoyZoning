using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JsdpDoctorService
{
    public JsdpDoctorReport Run(string workspaceRoot, JSDPStateStore store, JSDPLedger ledger)
    {
        var checks = new List<JsdpDoctorCheck>();
        void Add(string id, string status, string detail)
        {
            checks.Add(new JsdpDoctorCheck { Id = id, Status = status, Detail = detail });
        }

        var required = new[]
        {
            (JsdpPaths.Run(workspaceRoot), "run.json"),
            (JsdpPaths.ProjectSpec(workspaceRoot), "project-spec.json"),
            (JsdpPaths.Tree(workspaceRoot), "tree.json"),
            (JsdpPaths.Ledger(workspaceRoot), "ledger.jsonl"),
            (JsdpPaths.Config(workspaceRoot), "config.json"),
        };

        foreach (var (path, name) in required)
        {
            var optional = name == "config.json";
            if (!File.Exists(path))
            {
                Add($"file_{name}", optional ? "warn" : "fail", optional ? $"{name} missing — run jsdp analyze to regenerate" : $"Missing {name}");
                continue;
            }

            Add($"file_{name}", "ok", $"{name} present");
        }

        if (!store.IsInitialized)
        {
            Add("run_initialized", "fail", "No JSDP run — run jz jsdp init");
            return BuildReport(checks);
        }

        JsdpRun run;
        try
        {
            run = store.LoadRun();
        }
        catch (Exception ex)
        {
            Add("run_parse", "fail", ex.Message);
            return BuildReport(checks);
        }

        Add("run_parse", "ok", $"Run {run.Id} loaded with {run.Nodes.Count} nodes");

        try
        {
            _ = PromptDAGBuilder.TopologicalOrder(run.Nodes.Values);
            Add("dag_acyclic", "ok", "DAG is acyclic");
        }
        catch (JsdpException ex)
        {
            Add("dag_acyclic", "fail", ex.Message);
        }

        foreach (var node in run.Nodes.Values)
        {
            foreach (var dep in node.Dependencies)
            {
                if (!run.Nodes.ContainsKey(dep))
                    Add($"dep_{node.Id}_{dep}", "fail", $"Node {node.Id} missing dependency {dep}");
            }
        }

        var blockedDeps = FindBlockedDependencyChains(run);
        foreach (var msg in blockedDeps)
            Add("blocked_chain", "warn", msg);

        foreach (var node in run.Nodes.Values)
        {
            if (node.VerificationCommands.Count == 0)
                Add($"verify_cmds_{node.Id}", "fail", $"Node {node.Id} has no verification commands");
            else
                Add($"verify_cmds_{node.Id}", "ok", $"Node {node.Id}: {node.VerificationCommands.Count} command(s)");
        }

        if (!string.IsNullOrEmpty(run.CurrentNodeId))
        {
            if (!run.Nodes.TryGetValue(run.CurrentNodeId, out var current))
                Add("current_node", "fail", $"currentNodeId {run.CurrentNodeId} not in DAG");
            else if (current.Status is JsdpNodeStatus.Verified && PromptDAGBuilder.FindNextReadyNode(run) is not null)
                Add("current_node", "warn", $"currentNodeId {run.CurrentNodeId} is verified but a next node is ready — run jsdp continue");
            else
                Add("current_node", "ok", $"currentNodeId {run.CurrentNodeId} ({current.Status})");
        }
        else
        {
            Add("current_node", "ok", "No current node set");
        }

        CheckExternalPlanning(workspaceRoot, run, Add);
        CheckHorizonPlanning(workspaceRoot, run, Add);
        CheckOrphanPrompts(workspaceRoot, run, Add);
        CheckRunTreeSync(workspaceRoot, store, Add);

        return BuildReport(checks);
    }

    private static void CheckExternalPlanning(string workspaceRoot, JsdpRun run, Action<string, string, string> add)
    {
        var ctxPath = JsdpPaths.PlanningContext(workspaceRoot);
        var runPath = JsdpPaths.Run(workspaceRoot);
        if (File.Exists(ctxPath))
        {
            add("planning_context", "ok", "planning-context.json present");
            if (File.Exists(runPath))
            {
                var ctxTime = File.GetLastWriteTimeUtc(ctxPath);
                var runTime = File.GetLastWriteTimeUtc(runPath);
                if (runTime > ctxTime.AddSeconds(2))
                    add("planning_context_stale", "warn",
                        "run.json is newer than planning-context.json — re-export before external replanning");
            }
        }
        else if (run.Nodes.Count == 0)
            add("planning_context", "warn", "No DAG and no planning-context.json — run export-planning-context or plan");
        else
            add("planning_context", "ok", "DAG present (planning-context optional)");

        var schemaPath = JsdpPaths.PlanSchema(workspaceRoot);
        if (File.Exists(schemaPath))
            add("plan_schema", "ok", "plan-schema.json present");
        else
            add("plan_schema", "warn", "plan-schema.json missing — run planning-prompt");

        var promptPath = JsdpPaths.PlanningPromptFile(workspaceRoot);
        if (File.Exists(promptPath))
            add("planning_prompt", "ok", "planning-external.md present");
        else
            add("planning_prompt", "warn", "planning-external.md missing — run planning-prompt");
    }

    private static void CheckHorizonPlanning(string workspaceRoot, JsdpRun run, Action<string, string, string> add)
    {
        var ctxPath = JsdpPaths.HorizonContext(workspaceRoot);
        var runPath = JsdpPaths.Run(workspaceRoot);

        if (!File.Exists(ctxPath))
        {
            if (run.Nodes.Count > 0)
                add("horizon_context", "warn", "No horizon-context.json — run jz jsdp horizon export before rolling horizon");
            return;
        }

        add("horizon_context", "ok", "horizon-context.json present");

        if (File.Exists(runPath))
        {
            var ctxTime = File.GetLastWriteTimeUtc(ctxPath);
            var runTime = File.GetLastWriteTimeUtc(runPath);
            if (runTime > ctxTime.AddSeconds(2))
                add("horizon_context_stale", "warn",
                    "run.json is newer than horizon-context.json — re-export before validate/import");
        }

        var failed = run.Nodes.Values.Count(n => n.Status == JsdpNodeStatus.Failed);
        if (failed > 0)
            add("horizon_failures", "warn", $"{failed} failed node(s) — repair before horizon import");

        if (!File.Exists(JsdpPaths.HorizonSchema(workspaceRoot)))
            add("horizon_schema", "warn", "horizon-schema.json missing — run jz jsdp horizon prompt");

        if (File.Exists(ctxPath))
        {
            var bytes = new FileInfo(ctxPath).Length;
            if (bytes > JsdpContract.MaxHorizonContextBytes)
                add("horizon_context_size", "warn",
                    $"horizon-context.json is {bytes} bytes (budget {JsdpContract.MaxHorizonContextBytes})");
            else
                add("horizon_context_size", "ok", $"horizon-context.json {bytes} bytes within budget");
        }
    }

    private static void CheckOrphanPrompts(string workspaceRoot, JsdpRun run, Action<string, string, string> add)
    {
        var promptsDir = JsdpPaths.Prompts(workspaceRoot);
        if (!Directory.Exists(promptsDir))
            return;

        foreach (var file in Directory.EnumerateFiles(promptsDir, "*.md"))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, JsdpPaths.PlanningPromptFileName, StringComparison.Ordinal)
                || string.Equals(name, JsdpPaths.HorizonPromptFileName, StringComparison.Ordinal))
                continue;

            var id = Path.GetFileNameWithoutExtension(name);
            if (!run.Nodes.ContainsKey(id))
                add($"orphan_prompt_{id}", "warn", $"Prompt {name} has no matching DAG node — stale after replan/import");
        }
    }

    private static void CheckRunTreeSync(string workspaceRoot, JSDPStateStore store, Action<string, string, string> add)
    {
        var treePath = JsdpPaths.Tree(workspaceRoot);
        if (!File.Exists(treePath))
        {
            add("run_tree_sync", "warn", "tree.json missing — run jsdp plan or import-plan");
            return;
        }

        try
        {
            var run = store.LoadRun();
            var tree = store.LoadTree();
            if (tree.RunId != run.Id)
                add("run_tree_sync", "fail", "tree.json runId does not match run.json");
            else if (tree.Nodes.Count != run.Nodes.Count)
                add("run_tree_sync", "warn",
                    $"tree.json node count ({tree.Nodes.Count}) differs from run.json ({run.Nodes.Count})");
            else
                add("run_tree_sync", "ok", "run.json and tree.json in sync");
        }
        catch (Exception ex)
        {
            add("run_tree_sync", "fail", ex.Message);
        }
    }

    private static IReadOnlyList<string> FindBlockedDependencyChains(JsdpRun run)
    {
        var messages = new List<string>();
        foreach (var node in run.Nodes.Values.Where(n => n.Status == JsdpNodeStatus.Blocked))
        {
            var dependents = run.Nodes.Values.Count(n => n.Dependencies.Contains(node.Id, StringComparer.Ordinal));
            if (dependents > 0)
                messages.Add($"Blocked node {node.Id} blocks {dependents} downstream node(s)");
        }

        return messages;
    }

    private static JsdpDoctorReport BuildReport(List<JsdpDoctorCheck> checks)
    {
        var failed = checks.Count(c => c.Status == "fail");
        var warned = checks.Count(c => c.Status == "warn");
        return new JsdpDoctorReport
        {
            Ok = failed == 0,
            Warned = warned > 0,
            Checks = checks,
        };
    }
}

public sealed class JsdpDoctorReport
{
    public bool Ok { get; set; }
    public bool Warned { get; set; }
    public List<JsdpDoctorCheck> Checks { get; set; } = [];
}

public sealed class JsdpDoctorCheck
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
}
