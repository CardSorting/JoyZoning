using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class OperatorDecisionSafetyTests
{
    [Fact]
    public void Conflict_worker_approve_is_blocked()
    {
        var worker = Worker(
            mergeState: "merge_conflict",
            conflict: new MergeConflictDetail("overlapping_files", "overlap", ["src/a.cs"]));

        var (summary, approve, _) = OperatorDecisionSafety.BuildForWorker(worker);

        Assert.True(approve.Blocked);
        Assert.NotEmpty(approve.BlockReasons);
        Assert.True(summary.RiskFlags.HasConflicts);
    }

    [Fact]
    public void Failed_verification_sets_risk_flag_and_approve_warning()
    {
        var worker = Worker(
            mergeState: "merge_failed",
            readiness: Readiness(verificationPassed: false, testsRun: true));

        var (summary, approve, _) = OperatorDecisionSafety.BuildForWorker(worker);

        Assert.True(summary.RiskFlags.VerificationFailed);
        Assert.Contains(summary.RiskFlags.ActiveFlagNames(), n => n == "verification_failed");
        Assert.True(approve.Blocked);
    }

    [Fact]
    public void Missing_verification_sets_risk_flag_and_warns_on_approve()
    {
        var worker = Worker(
            mergeState: "ready_to_merge",
            leaseStatus: ExecutionLeaseStatus.ReadyForReview.ToString(),
            readiness: Readiness(verificationPassed: null, testsRun: false));

        var (summary, approve, _) = OperatorDecisionSafety.BuildForWorker(worker);

        Assert.True(summary.RiskFlags.VerificationMissing);
        Assert.False(approve.Blocked);
        Assert.True(approve.RequiresAcknowledgement);
        Assert.Contains(approve.Warnings, w => w.Contains("verification", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Revoke_with_changed_files_warns_but_does_not_block()
    {
        var worker = Worker(
            mergeState: "running",
            readiness: Readiness(changedCount: 3, summary: ["a.cs", "b.cs", "c.cs"]));

        var (summary, _, revoke) = OperatorDecisionSafety.BuildForWorker(worker);

        Assert.False(revoke.Blocked);
        Assert.True(revoke.RequiresAcknowledgement);
        Assert.Contains(revoke.Warnings, w => w.Contains("3 changed file", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(summary.WorktreePath));
    }

    [Fact]
    public void Large_change_set_sets_risk_flag()
    {
        var files = Enumerable.Range(0, 25).Select(i => $"src/f{i}.cs").ToList();
        var worker = Worker(
            mergeState: "ready_to_merge",
            readiness: Readiness(changedCount: 25, summary: files.Take(8).ToList()));

        var (summary, approve, _) = OperatorDecisionSafety.BuildForWorker(worker, largeChangeSetThreshold: 20);

        Assert.True(summary.RiskFlags.LargeChangeSet);
        Assert.Contains(approve.Warnings, w => w.Contains("Large change set", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Overlap_ready_workers_sets_overlap_flag()
    {
        var worker = Worker(
            mergeState: "merge_conflict",
            conflict: new MergeConflictDetail("overlapping_files", "overlap", ["shared.cs"]));

        var (summary, _, _) = OperatorDecisionSafety.BuildForWorker(worker);

        Assert.True(summary.RiskFlags.OverlapsWithOtherReadyWorker);
    }

    [Fact]
    public void Decision_summary_includes_mirror_and_worktree_paths()
    {
        var worker = Worker(
            worktree: "/wt/path",
            mirror: "/mirror/path",
            readiness: Readiness(changedCount: 1, summary: ["x.cs"]));

        var (summary, _, _) = OperatorDecisionSafety.BuildForWorker(worker);

        Assert.Equal("/wt/path", summary.WorktreePath);
        Assert.Equal("/mirror/path", summary.LiveMirrorPath);
    }

    [Fact]
    public void Revoked_worker_still_surfaces_paths_in_summary()
    {
        var worker = Worker(
            mergeState: "revoked",
            leaseStatus: ExecutionLeaseStatus.Revoked.ToString(),
            worktree: "/wt/revoked",
            mirror: "/mirror/revoked");

        var (summary, _, revoke) = OperatorDecisionSafety.BuildForWorker(worker);

        Assert.Equal("/wt/revoked", summary.WorktreePath);
        Assert.Equal("/mirror/revoked", summary.LiveMirrorPath);
        Assert.False(revoke.Blocked);
    }

    private static ParallelWorkerMirrorEntry Worker(
        string mergeState = "ready_to_merge",
        string leaseStatus = "ReadyForReview",
        WorkerMergeReadiness? readiness = null,
        MergeConflictDetail? conflict = null,
        string? worktree = "/tmp/wt",
        string? mirror = "/tmp/mirror")
    {
        readiness ??= Readiness();
        var entry = new ParallelWorkerMirrorEntry(
            TaskId: Guid.NewGuid(),
            TaskTitle: "test-card",
            ExecutionSessionId: Guid.NewGuid(),
            LeaseId: Guid.NewGuid(),
            HermesSessionId: "hermes-1",
            LiveMirrorPath: mirror,
            LiveMarkdownPath: null,
            HealthState: "active",
            LifecycleStatus: "active",
            LastMirroredAt: DateTimeOffset.UtcNow,
            KanbanRevision: 1,
            KanbanPushedRevision: 1,
            KanbanStatus: "NeedsApproval",
            LeaseStatus: leaseStatus,
            WorktreePath: worktree,
            IsSharedSessionRootMirror: false,
            RegistryCollision: null,
            MergeState: mergeState,
            MergeReadiness: readiness,
            MergeConflict: conflict,
            DecisionSummary: null!,
            ApproveGuardrails: null!,
            RevokeGuardrails: null!,
            RecommendedModeSlug: mergeState,
            AvailableModeTransitions: Array.Empty<ModeTransitionHint>(),
            AuthorityProfileSlug: "BalancedAuto",
            Authority: null);

        var (summary, approve, revoke) = OperatorDecisionSafety.BuildForWorker(entry);
        return entry with
        {
            DecisionSummary = summary,
            ApproveGuardrails = approve,
            RevokeGuardrails = revoke,
        };
    }

    private static WorkerMergeReadiness Readiness(
        int changedCount = 1,
        IReadOnlyList<string>? summary = null,
        bool? verificationPassed = true,
        bool? testsRun = true) =>
        new(
            ExecutionSessionId: Guid.NewGuid(),
            WorktreePath: "/tmp/wt",
            LiveMirrorPath: "/tmp/mirror",
            MergeTargetWorkspaceRoot: "/workspace",
            MergeTargetBranch: "main",
            HeadCommit: "abc1234",
            BaseCommit: "def5678",
            IsDirty: changedCount > 0,
            ChangedFilesCount: changedCount,
            ChangedFilesSummary: summary ?? ["file.cs"],
            VerificationPassed: verificationPassed,
            TestsRun: testsRun,
            VerificationSummary: verificationPassed == true ? "1/1 commands passed" : null);
}
