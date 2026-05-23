using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkerMergeStateResolverTests
{
    [Fact]
    public void Ready_for_review_with_passing_report_is_ready_to_merge()
    {
        var lease = Lease(ExecutionLeaseStatus.ReadyForReview, withPassingReport: true);
        var (state, conflict) = WorkerMergeStateResolver.Resolve(
            lease,
            Task(),
            executionPhase: null,
            mirrorLifecycleStatus: "active",
            persistedMergeState: null,
            passingReport: Deserialize(lease.VerificationReportJson),
            failedReport: null,
            hasOverlappingFilesWithOtherReadyWorkers: false,
            gitConflictPaths: Array.Empty<string>());

        Assert.Equal(WorkerMergeState.ReadyToMerge, state);
        Assert.Null(conflict);
    }

    [Fact]
    public void Merged_lease_is_merged()
    {
        var lease = Lease(ExecutionLeaseStatus.Merged);
        var (state, _) = WorkerMergeStateResolver.Resolve(
            lease,
            Task(),
            null,
            "completed",
            null,
            null,
            null,
            false,
            Array.Empty<string>());

        Assert.Equal(WorkerMergeState.Merged, state);
    }

    [Fact]
    public void Revoked_lease_is_revoked()
    {
        var lease = Lease(ExecutionLeaseStatus.Revoked);
        var (state, _) = WorkerMergeStateResolver.Resolve(
            lease,
            Task(),
            null,
            "completed",
            null,
            null,
            null,
            false,
            Array.Empty<string>());

        Assert.Equal(WorkerMergeState.Revoked, state);
    }

    [Fact]
    public void Overlapping_files_surface_merge_conflict()
    {
        var lease = Lease(ExecutionLeaseStatus.ReadyForReview, withPassingReport: true);
        var (state, conflict) = WorkerMergeStateResolver.Resolve(
            lease,
            Task(),
            null,
            "active",
            null,
            Deserialize(lease.VerificationReportJson),
            null,
            hasOverlappingFilesWithOtherReadyWorkers: true,
            gitConflictPaths: Array.Empty<string>());

        Assert.Equal(WorkerMergeState.MergeConflict, state);
        Assert.NotNull(conflict);
        Assert.Equal("overlapping_files", conflict!.Category);
    }

    [Fact]
    public void Merge_conflict_blocks_prune()
    {
        Assert.True(WorkerMergeStateResolver.BlocksPrune(WorkerMergeState.MergeConflict));
        Assert.True(WorkerMergeStateResolver.BlocksPrune(WorkerMergeState.MergeFailed));
        Assert.False(WorkerMergeStateResolver.BlocksPrune(WorkerMergeState.Merged));
    }

    private static WorkTask Task() => new()
    {
        Id = Guid.NewGuid(),
        Title = "t",
        Status = WorkTaskStatus.NeedsApproval,
    };

    private static ExecutionLease Lease(
        ExecutionLeaseStatus status,
        bool withPassingReport = false)
    {
        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = Guid.NewGuid(),
            Status = status,
            WorktreePath = "/tmp/wt",
        };

        if (withPassingReport)
        {
            lease.VerificationReportJson = VerificationReportSerializer.Serialize(new VerificationReport
            {
                CardId = lease.WorkTaskId,
                SessionId = Guid.NewGuid(),
                ChangedFiles = ["src/shared.cs"],
                CommandsRun = [new CommandRunSummary { Command = "dotnet test", Passed = true, Summary = "ok" }],
                ReadyForHumanReview = true,
            });
        }

        return lease;
    }

    private static VerificationReport? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : VerificationReportSerializer.Deserialize(json);
}
