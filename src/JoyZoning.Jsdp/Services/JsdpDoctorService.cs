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

        return BuildReport(checks);
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
