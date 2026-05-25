using System.Text.Json.Serialization;

namespace JoyZoning.Jsdp.Models;

public sealed class ProjectSpecDocument
{
    public string Goal { get; set; } = "";
    public string? SpecPath { get; set; }
    public string? RawMarkdown { get; set; }
    public ProjectSpecAnalysis? Analysis { get; set; }
    public RepoScanSnapshot? RepoScan { get; set; }
}

public sealed class ProjectSpecAnalysis
{
    public string ProductGoal { get; set; } = "";
    public List<string> TechStack { get; set; } = [];
    public List<string> DomainConcepts { get; set; } = [];
    public List<string> CoreSystems { get; set; } = [];
    public List<string> Constraints { get; set; } = [];
    public List<string> NonGoals { get; set; } = [];
    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<string> VerificationStrategy { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JsdpPlanningMode
{
    VerticalSlices,
    SystemsFirst,
    RiskFirst,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum JsdpNodeStatus
{
    Pending,
    Running,
    Blocked,
    Verified,
    Failed,
}

public sealed class JsdpNode
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Prompt { get; set; } = "";
    public List<string> Dependencies { get; set; } = [];
    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<string> VerificationCommands { get; set; } = [];
    public List<string> AllowedMutationSurface { get; set; } = [];
    public JsdpNodeStatus Status { get; set; } = JsdpNodeStatus.Pending;
    public List<string> Outputs { get; set; } = [];
    public string? RepairOf { get; set; }
    public List<string>? Next { get; set; }
}

public sealed class JsdpRun
{
    public string Id { get; set; } = "";
    public string Goal { get; set; } = "";
    public JsdpPlanningMode PlanningMode { get; set; } = JsdpPlanningMode.VerticalSlices;
    public string CreatedAt { get; set; } = "";
    public string? CurrentNodeId { get; set; }
    public Dictionary<string, JsdpNode> Nodes { get; set; } = new(StringComparer.Ordinal);
}

public sealed class LedgerEntry
{
    public string Timestamp { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<string> FilesChanged { get; set; } = [];
    public LedgerVerification Verification { get; set; } = new();
    public string? NextRecommendation { get; set; }
}

public sealed class LedgerVerification
{
    public List<string> CommandsRun { get; set; } = [];
    public bool Passed { get; set; }
    public List<string> Failures { get; set; } = [];
}

public sealed class JsdpTreeDocument
{
    public string RunId { get; set; } = "";
    public string Goal { get; set; } = "";
    public JsdpPlanningMode PlanningMode { get; set; }
    public List<JsdpTreeNodeSummary> Nodes { get; set; } = [];
    public JsdpConvergenceSummary Convergence { get; set; } = new();
}

public sealed class JsdpTreeNodeSummary
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public JsdpNodeStatus Status { get; set; }
    public List<string> Dependencies { get; set; } = [];
    public string? RepairOf { get; set; }
}

public sealed class JsdpConvergenceSummary
{
    public int Total { get; set; }
    public int Verified { get; set; }
    public int Failed { get; set; }
    public int Blocked { get; set; }
    public int Pending { get; set; }
    public int Running { get; set; }
    public string Health { get; set; } = "unknown";
}

public sealed class JsdpStatusReport
{
    public JsdpRun Run { get; set; } = new();
    public JsdpConvergenceSummary Convergence { get; set; } = new();
    public string? NextReadyNodeId { get; set; }
    public List<string> VerifiedNodeIds { get; set; } = [];
    public List<string> FailedNodeIds { get; set; } = [];
    public List<string> BlockedNodeIds { get; set; } = [];
}

public sealed class VerificationReport
{
    public string NodeId { get; set; } = "";
    public bool Passed { get; set; }
    public List<VerificationCommandResult> Results { get; set; } = [];
    public List<string> Failures { get; set; } = [];
    public string GeneratedAt { get; set; } = "";
}

public sealed class VerificationCommandResult
{
    public string Command { get; set; } = "";
    public int ExitCode { get; set; }
    public bool Passed { get; set; }
    public string Output { get; set; } = "";
}
