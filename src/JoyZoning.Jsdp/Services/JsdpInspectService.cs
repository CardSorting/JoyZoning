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
        report.ExternalPlanning = new JsdpInspectExternalPlanning
        {
            PlanningContextPath = JsdpPaths.PlanningContext(workspaceRoot),
            PlanningContextExists = File.Exists(JsdpPaths.PlanningContext(workspaceRoot)),
            PlanSchemaPath = JsdpPaths.PlanSchema(workspaceRoot),
            PlanSchemaExists = File.Exists(JsdpPaths.PlanSchema(workspaceRoot)),
            PlanningPromptPath = JsdpPaths.PlanningPromptFile(workspaceRoot),
            PlanningPromptExists = File.Exists(JsdpPaths.PlanningPromptFile(workspaceRoot)),
        };

        var horizonCtxPath = JsdpPaths.HorizonContext(workspaceRoot);
        var horizonStale = false;
        if (File.Exists(horizonCtxPath) && File.Exists(JsdpPaths.Run(workspaceRoot)))
        {
            horizonStale = File.GetLastWriteTimeUtc(JsdpPaths.Run(workspaceRoot))
                > File.GetLastWriteTimeUtc(horizonCtxPath).AddSeconds(2);
        }

        report.HorizonPlanning = new JsdpInspectHorizonPlanning
        {
            HorizonContextPath = horizonCtxPath,
            HorizonContextExists = File.Exists(horizonCtxPath),
            HorizonContextStale = horizonStale,
            HorizonContextBytes = File.Exists(horizonCtxPath) ? (int)new FileInfo(horizonCtxPath).Length : 0,
            HorizonPromptPath = JsdpPaths.HorizonPromptFile(workspaceRoot),
            HorizonPromptExists = File.Exists(JsdpPaths.HorizonPromptFile(workspaceRoot)),
            HorizonSchemaExists = File.Exists(JsdpPaths.HorizonSchema(workspaceRoot)),
            LastImport = File.Exists(JsdpPaths.HorizonLastImport(workspaceRoot))
                ? JsdpJson.ReadFile<JsdpHorizonLastImport>(JsdpPaths.HorizonLastImport(workspaceRoot))
                : null,
        };

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
    public JsdpInspectExternalPlanning? ExternalPlanning { get; set; }
    public JsdpInspectHorizonPlanning? HorizonPlanning { get; set; }
}

public sealed class JsdpInspectHorizonPlanning
{
    public string HorizonContextPath { get; set; } = "";
    public bool HorizonContextExists { get; set; }
    public bool HorizonContextStale { get; set; }
    public int HorizonContextBytes { get; set; }
    public string HorizonPromptPath { get; set; } = "";
    public bool HorizonPromptExists { get; set; }
    public bool HorizonSchemaExists { get; set; }
    public JsdpHorizonLastImport? LastImport { get; set; }
}

public sealed class JsdpInspectExternalPlanning
{
    public string PlanningContextPath { get; set; } = "";
    public bool PlanningContextExists { get; set; }
    public string PlanSchemaPath { get; set; } = "";
    public bool PlanSchemaExists { get; set; }
    public string PlanningPromptPath { get; set; } = "";
    public bool PlanningPromptExists { get; set; }
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
