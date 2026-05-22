using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

public class KanbanExecutionRulesTests
{
    private static WorkTask Task(RiskLevel risk = RiskLevel.Low) => new()
    {
        Id = Guid.NewGuid(),
        OperatorSessionId = Guid.NewGuid(),
        Title = "Test",
        Description = "Do the thing",
        Risk = risk,
        Status = WorkTaskStatus.Planned,
    };

    private static ExecutionLease Lease(LeaseRiskLevel risk, ExecutionLeaseStatus status) => new()
    {
        Id = Guid.NewGuid(),
        WorkTaskId = Guid.NewGuid(),
        OperatorSessionId = Guid.NewGuid(),
        WorktreePath = "/tmp/wt",
        BranchName = "joyzoning/card-abc",
        RiskLevel = risk,
        Status = status,
        StartedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
    };

    [Fact]
    public void Cannot_create_two_leases_when_active_exists()
    {
        var task = Task();
        var active = Lease(LeaseRiskLevel.Low, ExecutionLeaseStatus.Running);
        active.WorkTaskId = task.Id;

        var error = KanbanExecutionRules.ValidateNewLease(task, active, null, false);
        Assert.NotNull(error);
    }

    [Fact]
    public void Critical_requires_approval_and_blocks_global_slot()
    {
        var task = Task(RiskLevel.Critical);
        var occupant = Lease(LeaseRiskLevel.Critical, ExecutionLeaseStatus.Leased);
        occupant.CriticalApprovalGranted = true;

        Assert.Contains("approval", KanbanExecutionRules.ValidateNewLease(task, null, null, false)!);
        Assert.Contains("critical", KanbanExecutionRules.ValidateNewLease(task, null, occupant, true)!);
    }

    [Fact]
    public void Critical_approval_cannot_be_reused_after_consumed()
    {
        var lease = Lease(LeaseRiskLevel.Critical, ExecutionLeaseStatus.Leased);
        lease.CriticalApprovalGranted = true;
        lease.CriticalApprovalConsumed = true;

        Assert.Contains("consumed", KanbanExecutionRules.ValidateCriticalDispatch(lease)!);
    }

    [Fact]
    public void Terminal_statuses_cannot_transition()
    {
        Assert.Contains("terminal", KanbanExecutionRules.ValidateTransition(
            StatusChangeActor.DietCode,
            ExecutionLeaseStatus.Merged,
            ExecutionLeaseStatus.Running)!);
    }

    [Fact]
    public void Agent_cannot_transition_to_ready_for_review_directly()
    {
        var lease = Lease(LeaseRiskLevel.Low, ExecutionLeaseStatus.Verifying);
        var error = KanbanExecutionRules.ValidateTransition(
            StatusChangeActor.DietCode,
            lease.Status,
            ExecutionLeaseStatus.ReadyForReview)!;
        Assert.Contains("ReadyForReview", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Blocked_requires_reason()
    {
        var lease = Lease(LeaseRiskLevel.Low, ExecutionLeaseStatus.Running);
        Assert.Contains("reason", KanbanExecutionRules.ValidateAgentTransition(
            lease.Status, ExecutionLeaseStatus.Blocked, null)!);
    }

    [Fact]
    public void Agent_cannot_mark_done()
    {
        Assert.NotNull(KanbanExecutionRules.ValidateAgentTaskStatusChange(WorkTaskStatus.Complete));
    }

    [Fact]
    public void Failed_verification_cannot_move_to_review()
    {
        var lease = Lease(LeaseRiskLevel.Low, ExecutionLeaseStatus.Verifying);
        lease.WorkTaskId = Guid.NewGuid();

        var report = new VerificationReport
        {
            CardId = lease.WorkTaskId,
            SessionId = Guid.NewGuid(),
            CommandsRun =
            [
                new CommandRunSummary { Command = "dotnet test", Passed = false, Summary = "fail" },
            ],
            ReadyForHumanReview = true,
        };

        Assert.NotNull(VerificationReportValidator.ValidatePassingSubmission(lease, report));
    }

    [Fact]
    public void Human_merge_requires_passing_verification_report()
    {
        var lease = Lease(LeaseRiskLevel.Low, ExecutionLeaseStatus.ReadyForReview);
        Assert.Contains("verification", KanbanExecutionRules.ValidateHumanMerge(lease)!);

        lease.VerificationReportJson = VerificationReportSerializer.Serialize(new VerificationReport
        {
            CardId = lease.WorkTaskId,
            SessionId = Guid.NewGuid(),
            CommandsRun = [new CommandRunSummary { Command = "dotnet build", Passed = true, Summary = "ok" }],
            ReadyForHumanReview = true,
        });
        Assert.Null(KanbanExecutionRules.ValidateHumanMerge(lease));
    }

    [Fact]
    public void Cannot_replace_passing_report_without_supersede()
    {
        var passing = new VerificationReport
        {
            CardId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CommandsRun = [new CommandRunSummary { Command = "dotnet build", Passed = true, Summary = "ok" }],
            ReadyForHumanReview = true,
        };
        var weaker = new VerificationReport
        {
            CardId = passing.CardId,
            SessionId = Guid.NewGuid(),
            CommandsRun = [new CommandRunSummary { Command = "dotnet build", Passed = false, Summary = "fail" }],
            ReadyForHumanReview = false,
        };

        var json = VerificationReportSerializer.Serialize(passing);
        var replaceError = VerificationReportValidator.ValidateCanReplaceExistingPassingReport(
            json, weaker, supersede: false)!;
        Assert.NotNull(replaceError);
        Assert.True(
            replaceError.Contains("supersede", StringComparison.OrdinalIgnoreCase)
            || replaceError.Contains("replace", StringComparison.OrdinalIgnoreCase),
            replaceError);
    }
}
