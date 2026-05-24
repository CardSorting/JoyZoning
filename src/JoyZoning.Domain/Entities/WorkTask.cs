using System.Text.Json.Serialization;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

public class WorkTask
{
    public Guid Id { get; set; }
    public Guid OperatorSessionId { get; set; }
    public string? HermesKanbanTaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentKind AssignedAgent { get; set; } = AgentKind.DietCode;
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Backlog;
    public RiskLevel Risk { get; set; } = RiskLevel.Low;
    public VerificationState Verification { get; set; } = VerificationState.NotStarted;
    public string? LinkedRunId { get; set; }
    public string? LinkedApprovalId { get; set; }
    public string? LinkedTerminalSessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>Monotonic local revision; incremented on status changes.</summary>
    public long KanbanRevision { get; set; }
    /// <summary>Last revision successfully pushed to Hermes kanban.</summary>
    public long KanbanPushedRevision { get; set; }

    public TaskExecutionMode TaskExecutionMode { get; set; } = TaskExecutionMode.ManagedAgent;
    public ExecutionDriver ExecutionDriver { get; set; } = ExecutionDriver.Hermes;
    public string? ExternalAgentName { get; set; }
    public string? BranchName { get; set; }
    public string? WorkspacePath { get; set; }
    public DateTimeOffset? StartedExternallyAt { get; set; }
    public DateTimeOffset? ReadyForReviewAt { get; set; }
    public DateTimeOffset? LastWorkspaceScanAt { get; set; }
    public string? LastObservedCommit { get; set; }
    public bool HasUncommittedChanges { get; set; }
    public string? ChangedFilesJson { get; set; }
    public string? GeneratedPromptPath { get; set; }
    public string? GeneratedPromptText { get; set; }
    public bool VerificationRequired { get; set; } = true;
    public ExternalVerificationStatus ExternalVerificationStatus { get; set; } = ExternalVerificationStatus.NotRequired;
    public string? ExternalVerificationReportJson { get; set; }
    public bool MergeRequired { get; set; } = true;
    public bool ExternalMergeCompleted { get; set; }
    public DateTimeOffset? ExternalMergedAt { get; set; }

    [JsonIgnore]
    public OperatorSession? OperatorSession { get; set; }
    [JsonIgnore]
    public ICollection<ExecutionSession> Executions { get; set; } = new List<ExecutionSession>();
    [JsonIgnore]
    public ICollection<ApprovalRequest> Approvals { get; set; } = new List<ApprovalRequest>();
}
