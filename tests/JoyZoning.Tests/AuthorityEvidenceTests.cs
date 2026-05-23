using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.ControlPlane.Services;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class AuthorityEvidenceTests
{
    [Fact]
    public void ShouldAppendDecision_false_when_fingerprint_unchanged()
    {
        var decision = AuthorityPolicyEvaluator.Evaluate(SampleInput());
        const string mergeState = "ready_to_merge";
        string[] overlap = [];

        Assert.True(AuthorityEvidence.ShouldAppendDecision("[]", decision, mergeState, overlap));

        var lease = new ExecutionLease { EvidenceLogJson = "[]" };
        LeaseRuntimeService.AppendEvidence(
            lease,
            AuthorityEvidence.DecisionKind,
            StatusChangeActor.System,
            ExecutionLeaseStatus.ReadyForReview,
            ExecutionLeaseStatus.ReadyForReview,
            detail: AuthorityEvidence.ToEvidenceDetail(decision, true, mergeState, overlap));

        Assert.False(AuthorityEvidence.ShouldAppendDecision(
            lease.EvidenceLogJson, decision, mergeState, overlap));
    }

    [Fact]
    public void TryReadLast_prefers_auto_blocked_for_needs_review()
    {
        var lease = new ExecutionLease { EvidenceLogJson = "[]" };
        LeaseRuntimeService.AppendEvidence(
            lease,
            AuthorityEvidence.DecisionKind,
            StatusChangeActor.System,
            ExecutionLeaseStatus.ReadyForReview,
            ExecutionLeaseStatus.ReadyForReview,
            detail: new
            {
                profile = "BalancedAuto",
                autoAcceptAllowed = true,
                needsHumanReview = false,
                reasonCodes = new[] { "auto_accept_eligible" },
            });
        LeaseRuntimeService.AppendEvidence(
            lease,
            AuthorityEvidence.AutoBlockedKind,
            StatusChangeActor.System,
            ExecutionLeaseStatus.ReadyForReview,
            ExecutionLeaseStatus.ReadyForReview,
            detail: new
            {
                profile = "BalancedAuto",
                autoAcceptAllowed = false,
                needsHumanReview = true,
                reasonCodes = new[] { "overlapping_ready_worker" },
                humanMessages = new[] { "Blocked: overlapping worker diff (src/shared.cs)" },
            });

        var read = AuthorityEvidence.TryReadLast(lease.EvidenceLogJson);
        Assert.NotNull(read);
        Assert.False(read!.AutoAcceptAllowed);
        Assert.True(read.NeedsHumanReview);
        Assert.Contains("overlapping_ready_worker", read.ReasonCodes);
    }

    private static AuthorityEvaluationInput SampleInput() =>
        new(
            AuthorityProfileKind.BalancedAuto,
            AutopilotEnabled: true,
            MetadataOnlyAcceptResult: false,
            ExecutionLeaseStatus.ReadyForReview,
            RiskLevel.Low,
            "ready_to_merge",
            VerificationPassed: true,
            VerificationMissing: false,
            HasConflicts: false,
            OverlappingReadyWorker: false,
            MergeObservabilityUnknown: false,
            DirtyWorktree: false,
            UnknownHeadCommit: false,
            ChangedFilesCount: 1,
            ["docs/readme.md"],
            LargeChangeSetThreshold: 20);
}
