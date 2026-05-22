using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

public class ApprovalRequest
{
    public Guid Id { get; set; }
    public Guid? WorkTaskId { get; set; }
    public Guid? ExecutionSessionId { get; set; }
    public Guid? OperatorSessionId { get; set; }
    public string? HermesRunId { get; set; }
    public AgentKind RequestingAgent { get; set; }
    public ApprovalCategory Category { get; set; }
    public RiskLevel Risk { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ParametersJson { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public ApprovalScope? GrantedScope { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public WorkTask? WorkTask { get; set; }
    public ExecutionSession? ExecutionSession { get; set; }
    public OperatorSession? OperatorSession { get; set; }
}
