using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Tests;

internal static class JsdpTestFixtures
{
    public static ExecutionLease MergedLease(Guid taskId, Guid sessionId) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkTaskId = taskId,
            OperatorSessionId = sessionId,
            Status = ExecutionLeaseStatus.Merged,
            WorktreePath = "/tmp/wt",
            BranchName = "joyzoning/test",
            MergedAt = DateTimeOffset.UtcNow,
        };

    public static OperatorSession BoundedSession(Guid chainId, int sequence, string root = "/tmp/project") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"seq {sequence}",
            WorkspaceRoot = root,
            ExecutionMode = SessionExecutionMode.BoundedRole,
            DeliveryChainId = chainId,
            DeliverySequence = sequence,
        };

    public static WorkTask RoleTask(
        Guid sessionId,
        string title,
        WorkTaskStatus status = WorkTaskStatus.Planned,
        string? description = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            OperatorSessionId = sessionId,
            Title = title,
            Description = description ?? BuildCompliantDescription(title),
            Status = status,
        };

    public static string BuildCompliantDescription(string title) =>
        $"""
        {title}
        Goal: accomplish the role
        Scope: in-scope work only
        Planned Changes: listed files
        Risks: known risks
        Deliverables: concrete outputs
        Completion Criteria: done when verified
        Follow-Up Notes: deferred items
        """;
}
