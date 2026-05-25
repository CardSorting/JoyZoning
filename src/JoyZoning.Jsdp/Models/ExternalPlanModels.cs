namespace JoyZoning.Jsdp.Models;

/// <summary>Context bundle for external agents to author a plan (read-only contract).</summary>
public sealed class JsdpPlanningContext
{
    public string ExportedAt { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public string RunId { get; set; } = "";
    public string Goal { get; set; } = "";
    public JsdpPlanningMode PlanningMode { get; set; }
    public ProjectSpecAnalysis? SpecAnalysis { get; set; }
    public RepoScanSnapshot? RepoScan { get; set; }
    public JsdpConfig? Config { get; set; }
    public JsdpPlanningContextDag? ExistingDag { get; set; }
    public List<string> Constraints { get; set; } = [];
    public List<string> NonGoals { get; set; } = [];
    public List<string> AcceptanceCriteria { get; set; } = [];
    public JsdpPlanningContextPaths Paths { get; set; } = new();
}

public sealed class JsdpPlanningContextDag
{
    public int NodeCount { get; set; }
    public JsdpConvergenceSummary? Convergence { get; set; }
    public List<JsdpTreeNodeSummary> Nodes { get; set; } = [];
}

public sealed class JsdpPlanningContextPaths
{
    public string ProjectSpec { get; set; } = "";
    public string Config { get; set; } = "";
    public string PlanningContext { get; set; } = "";
    public string Run { get; set; } = "";
}

/// <summary>External agent-authored plan file (import/validate).</summary>
public sealed class ExternalJsdpPlanDocument
{
    public JsdpPlanningMode? PlanningMode { get; set; }
    public List<ExternalJsdpPlanNode> Nodes { get; set; } = [];
}

public sealed class ExternalJsdpPlanNode
{
    public string? Id { get; set; }
    public string Title { get; set; } = "";
    public string Intent { get; set; } = "";
    public string? Prompt { get; set; }
    public List<string> Dependencies { get; set; } = [];
    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<string> VerificationCommands { get; set; } = [];
    public List<string> AllowedMutationSurface { get; set; } = [];
    public List<string>? Outputs { get; set; }
    public string? RepairOf { get; set; }
}

public sealed class JsdpPlanValidationResult
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int NodeCount { get; set; }
    public Dictionary<string, JsdpNode>? NormalizedNodes { get; set; }
}

public sealed class JsdpExportPlanningContextResult
{
    public string Path { get; set; } = "";
    public JsdpPlanningContext Context { get; set; } = new();
}

public sealed class JsdpImportPlanResult
{
    public bool Imported { get; set; }
    public string PlanPath { get; set; } = "";
    public int NodeCount { get; set; }
    public JsdpPlanValidationResult Validation { get; set; } = new();
}

public sealed class JsdpPlanningPromptResult
{
    public string PromptPath { get; set; } = "";
    public string PlanningContextPath { get; set; } = "";
    public string SchemaPath { get; set; } = "";
}
