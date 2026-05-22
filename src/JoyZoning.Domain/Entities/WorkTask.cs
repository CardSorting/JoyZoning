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

    public OperatorSession? OperatorSession { get; set; }
    public ICollection<ExecutionSession> Executions { get; set; } = new List<ExecutionSession>();
    public ICollection<ApprovalRequest> Approvals { get; set; } = new List<ApprovalRequest>();
}
