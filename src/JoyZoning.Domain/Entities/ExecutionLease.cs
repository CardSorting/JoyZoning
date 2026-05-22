using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

/// <summary>Exclusive execution authority for one kanban card (WorkTask).</summary>
public class ExecutionLease
{
    public Guid Id { get; set; }
    public Guid WorkTaskId { get; set; }
    public Guid OperatorSessionId { get; set; }
    public Guid? ExecutionSessionId { get; set; }
    public string WorktreePath { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public ExecutionLeaseStatus Status { get; set; } = ExecutionLeaseStatus.Leased;
    public LeaseRiskLevel RiskLevel { get; set; } = LeaseRiskLevel.Low;
    public string AllowedPathsJson { get; set; } = "[]";
    public string ForbiddenPathsJson { get; set; } = "[]";
    public string HandoffPacketJson { get; set; } = "{}";
    public string? VerificationReportJson { get; set; }
    public string EvidenceLogJson { get; set; } = "[]";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? MergedAt { get; set; }
    public string? BlockedReason { get; set; }
    public bool CriticalApprovalGranted { get; set; }
    public bool CriticalApprovalConsumed { get; set; }
    public string? FailedVerificationReportJson { get; set; }
    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public StatusChangeActor CreatedBy { get; set; } = StatusChangeActor.System;
    public Guid AssignedSessionId { get; set; }
    public AgentKind AssignedAgent { get; set; } = AgentKind.DietCode;
    public int DispatchAttemptCount { get; set; }
    public Guid? RecoveredFromLeaseId { get; set; }

    public WorkTask? WorkTask { get; set; }
}
