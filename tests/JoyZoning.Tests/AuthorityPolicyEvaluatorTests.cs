using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class AuthorityPolicyEvaluatorTests
{
    [Fact]
    public void BalancedAuto_allows_low_risk_docs_change()
    {
        var decision = Evaluate(
            AuthorityProfileKind.BalancedAuto,
            files: ["docs/readme.md"],
            risk: RiskLevel.Low);

        Assert.True(decision.AutoAcceptAllowed);
        Assert.False(decision.NeedsHumanReview);
        Assert.Contains(AuthorityReasonCodes.AutoAcceptEligible, decision.ReasonCodes);
    }

    [Fact]
    public void BalancedAuto_blocks_auth_path()
    {
        var decision = Evaluate(
            AuthorityProfileKind.BalancedAuto,
            files: ["src/auth/login.ts"]);

        Assert.False(decision.AutoAcceptAllowed);
        Assert.True(decision.NeedsHumanReview);
        Assert.Contains(decision.HumanMessages, m => m.Contains("protected path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BalancedAuto_blocks_package_json()
    {
        var decision = Evaluate(
            AuthorityProfileKind.BalancedAuto,
            files: ["package.json"]);

        Assert.False(decision.AutoAcceptAllowed);
    }

    [Fact]
    public void Conservative_never_auto_accepts()
    {
        var decision = Evaluate(AuthorityProfileKind.Conservative, files: ["docs/note.md"]);
        Assert.False(decision.AutoAcceptAllowed);
        Assert.Contains(AuthorityReasonCodes.ProfileConservative, decision.ReasonCodes);
    }

    [Fact]
    public void Failed_verification_blocks()
    {
        var decision = Evaluate(
            AuthorityProfileKind.Yolo,
            files: ["docs/a.md"],
            verificationPassed: false);

        Assert.False(decision.AutoAcceptAllowed);
        Assert.Contains(decision.HumanMessages, m => m.Contains("verification failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Merge_conflict_blocks()
    {
        var decision = Evaluate(
            AuthorityProfileKind.Yolo,
            files: ["docs/a.md"],
            mergeState: "merge_conflict",
            hasConflicts: true);

        Assert.False(decision.AutoAcceptAllowed);
    }

    [Fact]
    public void Overlapping_ready_worker_blocks()
    {
        var decision = Evaluate(
            AuthorityProfileKind.BalancedAuto,
            files: ["src/shared.cs"],
            overlapping: true);

        Assert.False(decision.AutoAcceptAllowed);
        Assert.Contains(AuthorityReasonCodes.OverlappingReadyWorker, decision.ReasonCodes);
        Assert.Contains(decision.HumanMessages, m => m.Contains("overlapping", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Merge_observability_unknown_blocks()
    {
        var decision = Evaluate(
            AuthorityProfileKind.Yolo,
            files: ["docs/a.md"],
            mergeObservabilityUnknown: true);

        Assert.False(decision.AutoAcceptAllowed);
        Assert.Contains(AuthorityReasonCodes.MergeObservabilityUnknown, decision.ReasonCodes);
    }

    private static AuthorityDecision Evaluate(
        AuthorityProfileKind profile,
        IReadOnlyList<string>? files = null,
        RiskLevel risk = RiskLevel.Low,
        string mergeState = "ready_to_merge",
        bool verificationPassed = true,
        bool hasConflicts = false,
        bool overlapping = false,
        bool mergeObservabilityUnknown = false) =>
        AuthorityPolicyEvaluator.Evaluate(new AuthorityEvaluationInput(
            profile,
            AutopilotEnabled: true,
            MetadataOnlyAcceptResult: false,
            ExecutionLeaseStatus.ReadyForReview,
            risk,
            mergeState,
            verificationPassed,
            VerificationMissing: !verificationPassed,
            hasConflicts,
            OverlappingReadyWorker: overlapping,
            MergeObservabilityUnknown: mergeObservabilityUnknown,
            DirtyWorktree: false,
            UnknownHeadCommit: false,
            files?.Count ?? 0,
            files ?? Array.Empty<string>(),
            LargeChangeSetThreshold: 20));
}
