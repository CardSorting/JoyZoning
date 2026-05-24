using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class OperatorDecisionActionsTests
{
    [Theory]
    [InlineData("accept", "accept")]
    [InlineData("Accept", "accept")]
    [InlineData("approve", "accept")]
    [InlineData("APPROVE", "accept")]
    [InlineData("revoke", "revoke")]
    [InlineData("inspect", "inspect")]
    public void TryParse_normalizes_accept_and_legacy_approve(string input, string expected)
    {
        Assert.True(OperatorDecisionActions.TryParse(input, out var action));
        Assert.Equal(expected, action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("merge")]
    public void TryParse_rejects_unknown_actions(string? input)
    {
        Assert.False(OperatorDecisionActions.TryParse(input, out _));
    }

    [Fact]
    public void Approve_guardrails_use_accept_action_name()
    {
        var summary = new OperatorDecisionSummary(
            TaskId: Guid.NewGuid(),
            TaskTitle: "t",
            ExecutionSessionId: Guid.NewGuid(),
            LeaseId: Guid.NewGuid(),
            MergeState: "ready_to_merge",
            ChangedFilesCount: 1,
            ChangedFilesSummary: ["src/a.cs"],
            VerificationStatus: "passed",
            ConflictStatus: "none",
            BaseCommit: "abc",
            HeadCommit: "def",
            WorktreePath: "/wt",
            RiskFlags: new OperatorRiskFlags(
                false, false, false, false, false, false, false, false));

        var guardrails = OperatorDecisionSafety.BuildApproveGuardrails(summary);
        Assert.Equal(OperatorDecisionActions.Accept, guardrails.Action);
    }
}
