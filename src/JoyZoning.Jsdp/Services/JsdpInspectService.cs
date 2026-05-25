using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JsdpInspectService
{
    public JsdpInspectReport Build(
        JSDPStateStore store,
        JSDPLedger ledger,
        string workspaceRoot)
    {
        var report = new JsdpInspectReport
        {
            WorkspaceRoot = workspaceRoot,
            Config = store.IsInitialized ? store.LoadConfig() : null,
        };

        if (!store.IsInitialized)
            return report;

        var spec = store.LoadProjectSpec();
        report.SpecAnalysis = spec.Analysis;
        report.RepoScan = spec.RepoScan;

        var run = store.LoadRun();
        report.Run = new JsdpInspectRunSummary
        {
            Id = run.Id,
            Goal = run.Goal,
            PlanningMode = run.PlanningMode,
            CurrentNodeId = run.CurrentNodeId,
            Convergence = PromptDAGBuilder.ComputeConvergence(run),
        };
        report.Dag = PromptDAGBuilder.BuildTreeDocument(run);

        if (!string.IsNullOrEmpty(run.CurrentNodeId) && run.Nodes.TryGetValue(run.CurrentNodeId, out var current))
            report.CurrentNode = current;

        var entries = ledger.ReadAll();
        report.LastLedgerEntry = entries.Count > 0 ? entries[^1] : null;
        report.RepairLineage = BuildRepairLineage(run);

        return report;
    }

    private static List<JsdpRepairLineageEntry> BuildRepairLineage(JsdpRun run)
    {
        return run.Nodes.Values
            .Where(n => n.RepairOf is not null)
            .Select(n => new JsdpRepairLineageEntry
            {
                RepairNodeId = n.Id,
                OriginalNodeId = n.RepairOf!,
                Status = n.Status,
                Title = n.Title,
            })
            .OrderBy(e => e.RepairNodeId, StringComparer.Ordinal)
            .ToList();
    }
}

public sealed class JsdpInspectReport
{
    public string WorkspaceRoot { get; set; } = "";
    public ProjectSpecAnalysis? SpecAnalysis { get; set; }
    public RepoScanSnapshot? RepoScan { get; set; }
    public JsdpConfig? Config { get; set; }
    public JsdpInspectRunSummary? Run { get; set; }
    public JsdpTreeDocument? Dag { get; set; }
    public JsdpNode? CurrentNode { get; set; }
    public LedgerEntry? LastLedgerEntry { get; set; }
    public List<JsdpRepairLineageEntry> RepairLineage { get; set; } = [];
}

public sealed class JsdpInspectRunSummary
{
    public string Id { get; set; } = "";
    public string Goal { get; set; } = "";
    public JsdpPlanningMode PlanningMode { get; set; }
    public string? CurrentNodeId { get; set; }
    public JsdpConvergenceSummary Convergence { get; set; } = new();
}

public sealed class JsdpRepairLineageEntry
{
    public string RepairNodeId { get; set; } = "";
    public string OriginalNodeId { get; set; } = "";
    public JsdpNodeStatus Status { get; set; }
    public string Title { get; set; } = "";
}
