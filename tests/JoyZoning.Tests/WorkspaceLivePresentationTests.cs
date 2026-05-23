using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using StatusChangeActor = JoyZoning.Domain.Enums.StatusChangeActor;
using Xunit;

namespace JoyZoning.Tests;

public class WorkspaceLivePresentationTests
{
    [Fact]
    public void Running_with_recent_write_is_active_and_fast_poll()
    {
        var now = DateTimeOffset.UtcNow;
        var vm = WorkspaceLivePresentation.Build(
            "TinyQuest",
            nameof(ExecutionLeaseStatus.Running),
            blockedReason: null,
            evidenceLogJson: "[]",
            appScreens: 2,
            featureFiles: 5,
            sharedFiles: 3,
            hasPackageJson: true,
            hasReadme: false,
            worktreeFileCount: 40,
            worktreeLastWriteUtc: now.AddSeconds(-30),
            filesCopiedThisTick: 0,
            now);

        Assert.Equal(WorkspaceLivePresentation.ActivityActive, vm.ActivityState);
        Assert.True(vm.RecommendedPollSeconds <= 5);
        Assert.Equal(WorkspaceLivePresentation.PollBurst, vm.PollMode);
        Assert.Equal("Step 2 of 4", vm.StepProgressLabel);
        Assert.Contains("Building", vm.Headline);
    }

    [Fact]
    public void Stuck_activity_sets_slow_poll_and_warning()
    {
        var now = DateTimeOffset.UtcNow;
        var vm = WorkspaceLivePresentation.Build(
            "App",
            nameof(ExecutionLeaseStatus.Running),
            null,
            "[]",
            1, 1, 1, true, false, 10,
            now.AddMinutes(-8),
            0,
            now);

        Assert.Equal(WorkspaceLivePresentation.ActivityStuck, vm.ActivityState);
        Assert.Equal(WorkspaceLivePresentation.PollSlow, vm.PollMode);
        Assert.NotNull(vm.StaleWarning);
    }

    [Fact]
    public void Blocked_surfaces_next_actions_for_api_key()
    {
        var vm = WorkspaceLivePresentation.Build(
            "App",
            nameof(ExecutionLeaseStatus.Blocked),
            "401 invalid_api_key for Nous Portal",
            "[]",
            0, 0, 0, false, false, 0, null, 0);

        Assert.Equal(WorkspaceLivePresentation.ActivityBlocked, vm.ActivityState);
        Assert.Contains(vm.Steps, s => s.State == WorkspaceLivePresentation.StepFailed);
        Assert.True(vm.NextActions.Count >= 2);
        Assert.Contains("API key", vm.NextActions[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadyForReview_stops_polling()
    {
        var poll = WorkspaceLivePresentation.RecommendPollSeconds(
            nameof(ExecutionLeaseStatus.ReadyForReview),
            DateTimeOffset.UtcNow,
            filesCopiedThisTick: 0);

        Assert.Equal(0, poll);
    }

    [Fact]
    public void FriendlyEvidence_maps_dispatch_failed()
    {
        var entry = new LeaseEvidenceEntry(
            DateTimeOffset.UtcNow,
            "dispatch.failed",
            StatusChangeActor.System,
            nameof(ExecutionLeaseStatus.Leased),
            nameof(ExecutionLeaseStatus.Blocked),
            Reason: "401",
            Summary: "invalid key",
            Detail: null);

        var text = WorkspaceLivePresentation.FriendlyEvidence(entry);
        Assert.Contains("AI worker", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid key", text);
    }
}
