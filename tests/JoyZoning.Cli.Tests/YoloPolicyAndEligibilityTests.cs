using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class YoloPolicyAndEligibilityTests
{
    [Fact]
    public void Policy_rejects_allowRevoke_true()
    {
        var policy = ValidPolicy();
        policy.AllowRevoke = true;
        var ex = Assert.Throws<InvalidOperationException>(() => policy.Validate());
        Assert.Contains("allowRevoke", ex.Message);
    }

    [Fact]
    public void Policy_rejects_forbidden_verification_command()
    {
        var policy = ValidPolicy();
        var ex = Assert.Throws<YoloPolicyViolationException>(() =>
            policy.AssertCommandAllowed("git push origin main"));
        Assert.Contains("forbiddenCommands", ex.Message);
    }

    [Fact]
    public void Eligibility_includes_blocked_when_allowRecover()
    {
        var policy = ValidPolicy();
        policy.AllowRecover = true;
        Assert.Contains(WorkTaskStatus.Blocked, YoloEligibility.GetPickupStatuses(policy));
    }

    [Fact]
    public void Eligibility_blocked_recoverable_not_skipped_for_active_lease()
    {
        var policy = ValidPolicy();
        policy.AllowRecover = true;
        var candidate = YoloEligibility.Evaluate(
            Guid.NewGuid(),
            "Retry #test",
            "tags: test",
            (int)WorkTaskStatus.Blocked,
            (int)RiskLevel.Low,
            hasActiveLease: false,
            activeLeaseStatus: ExecutionLeaseStatus.Blocked,
            policy);
        Assert.True(candidate.IsEligible);
    }

    [Fact]
    public void Task_tags_extract_hashtag_and_tags_line()
    {
        var tags = YoloTaskTags.Extract("Fix docs #docs", "tags: test, docs\nBody");
        Assert.Contains("docs", tags);
        Assert.Contains("test", tags);
    }

    [Fact]
    public void Executor_wait_accepts_running_and_leased()
    {
        Assert.True(YoloExecutorWait.IsReadyForVerification(ExecutionLeaseStatus.Leased));
        Assert.True(YoloExecutorWait.IsReadyForVerification(ExecutionLeaseStatus.Running));
        Assert.True(YoloExecutorWait.IsTerminalFailure(ExecutionLeaseStatus.Revoked));
    }

    private static YoloPolicy ValidPolicy() => new()
    {
        Enabled = true,
        SessionId = Guid.NewGuid(),
        MaxRiskLevel = "medium",
        RequiredVerificationCommands = ["true"],
        RequireHumanMerge = true,
        AllowRevoke = false,
    };
}
