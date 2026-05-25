namespace JoyZoning.Jsdp.Models;

/// <summary>Bounded context for external rolling-horizon planning (no full DAG/spec/ledger).</summary>
public sealed class JsdpHorizonContext
{
    public string ContractVersion { get; set; } = JsdpContract.HorizonContextVersion;
    public string ExportedAt { get; set; } = "";
    public string ProjectSummary { get; set; } = "";
    public JsdpPlanningMode PlanningMode { get; set; }
    public JsdpHorizonFrontier CurrentFrontier { get; set; } = new();
    public List<JsdpHorizonLedgerSummary> RecentLedgerSummaries { get; set; } = [];
    public List<JsdpHorizonActiveFailure> ActiveFailures { get; set; } = [];
    public JsdpHorizonRepoSummary RepoSummary { get; set; } = new();
    public int RequestedNodeCount { get; set; }
    public List<string> ExistingNodeIds { get; set; } = [];
    public string RunId { get; set; } = "";
    public int DagSizeAtExport { get; set; }
    public string? PreviousStopAfter { get; set; }
    public string? PlanningGuidance { get; set; }
}

public sealed class JsdpHorizonFrontier
{
    public List<string> Verified { get; set; } = [];
    public List<string> Ready { get; set; } = [];
    public List<string> Blocked { get; set; } = [];
    public List<string> Failed { get; set; } = [];
}

public sealed class JsdpHorizonLedgerSummary
{
    public string NodeId { get; set; } = "";
    public string Summary { get; set; } = "";
    public bool VerificationPassed { get; set; }
}

public sealed class JsdpHorizonActiveFailure
{
    public string NodeId { get; set; } = "";
    public string FailureSummary { get; set; } = "";
}

public sealed class JsdpHorizonRepoSummary
{
    public List<string> DetectedStack { get; set; } = [];
    public List<string> ImportantPaths { get; set; } = [];
    public List<string> TestCommands { get; set; } = [];
}

public sealed class JsdpHorizonProposalDocument
{
    public string? ContractVersion { get; set; }
    public List<ExternalJsdpPlanNode> Nodes { get; set; } = [];
    public string Rationale { get; set; } = "";
    public List<string> Assumptions { get; set; } = [];
    public string StopAfter { get; set; } = "";
}

public sealed class JsdpHorizonValidationResult
{
    public bool Valid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int NodeCount { get; set; }
    public Dictionary<string, JsdpNode>? NormalizedNodes { get; set; }
}

public sealed class JsdpHorizonExportResult
{
    public string HorizonContextPath { get; set; } = "";
    public string ProjectSummaryPath { get; set; } = "";
    public string RepoSummaryPath { get; set; } = "";
    public string FrontierPath { get; set; } = "";
    public int ContextByteSize { get; set; }
    public bool ContextWithinBudget { get; set; }
    public List<string> Warnings { get; set; } = [];
    public JsdpHorizonContext Context { get; set; } = new();
}

public sealed class JsdpHorizonPromptResult
{
    public string PromptPath { get; set; } = "";
    public string HorizonContextPath { get; set; } = "";
    public string SchemaPath { get; set; } = "";
}

public sealed class JsdpHorizonImportResult
{
    public bool Imported { get; set; }
    public bool DryRun { get; set; }
    public string ProposalPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public int AppendedNodeCount { get; set; }
    public int TotalNodeCount { get; set; }
    public List<string> AppendedNodeIds { get; set; } = [];
    public List<string> ProjectedNodeIds { get; set; } = [];
    public bool ContextRefreshed { get; set; }
    public JsdpHorizonValidationResult Validation { get; set; } = new();
}

public sealed class JsdpHorizonStatusReport
{
    public bool HorizonContextStale { get; set; }
    public bool HasActiveFailures { get; set; }
    public bool HasReadyNodes { get; set; }
    public int ContextByteSize { get; set; }
    public string? SuggestedAction { get; set; }
    public int DagSize { get; set; }
    public List<string> ReadyNodeIds { get; set; } = [];
    public List<string> BlockedNodeIds { get; set; } = [];
    public List<string> FailedNodeIds { get; set; } = [];
    public JsdpHorizonLastImport? LastHorizonImport { get; set; }
    public int SuggestedNextHorizonSize { get; set; }
    public JsdpHorizonFrontier Frontier { get; set; } = new();
}

public sealed class JsdpHorizonLastImport
{
    public string ImportedAt { get; set; } = "";
    public string ProposalPath { get; set; } = "";
    public int AppendedNodes { get; set; }
    public List<string> NodeIds { get; set; } = [];
    public string StopAfter { get; set; } = "";
    public string Rationale { get; set; } = "";
}

public sealed class JsdpProjectSummaryDocument
{
    public string Summary { get; set; } = "";
    public string Goal { get; set; } = "";
    public string ExportedAt { get; set; } = "";
}

public sealed class JsdpRepoSummaryDocument
{
    public JsdpHorizonRepoSummary RepoSummary { get; set; } = new();
    public string ExportedAt { get; set; } = "";
}

public sealed class JsdpFrontierDocument
{
    public JsdpHorizonFrontier Frontier { get; set; } = new();
    public string ExportedAt { get; set; } = "";
}
