using System.Text.Json;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceLiveModeNavigationTests
{
    [Fact]
    public void Live_task_body_includes_mode_navigation_with_recommended_mode()
    {
        var body = WorkspaceLiveResponseBuilder.ToApiBody(ShellSnapshot(leaseStatus: "ReadyForReview"));
        var json = JsonSerializer.Serialize(body);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("modeNavigation", out var nav));
        Assert.Equal("review", nav.GetProperty("recommendedMode").GetString());
        Assert.True(nav.TryGetProperty("availableTransitions", out var transitions));
        Assert.True(transitions.GetArrayLength() > 0);
    }

    [Fact]
    public void Idle_body_still_exposes_mode_navigation()
    {
        var body = WorkspaceLiveResponseBuilder.IdleBody(Guid.NewGuid(), "idle", "waiting");
        var json = JsonSerializer.Serialize(body);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("modeNavigation", out var nav));
        Assert.False(string.IsNullOrWhiteSpace(nav.GetProperty("recommendedMode").GetString()));
    }

    [Fact]
    public void ForWorker_enriches_recommended_mode_on_merge_ready_worker()
    {
        var worker = ShellWorker(mergeState: "ready_to_merge", leaseStatus: "ReadyForReview");
        Assert.Equal(OperationalModeNavigation.SlugReview, worker.RecommendedModeSlug);
        Assert.Contains(worker.AvailableModeTransitions, t => t.TargetModeSlug == OperationalModeNavigation.SlugExecution);
        Assert.Contains(worker.AvailableModeTransitions, t => t.TargetModeSlug == OperationalModeNavigation.SlugReview);
    }

    private static WorkspaceLiveSnapshot ShellSnapshot(string leaseStatus)
    {
        var presentation = WorkspaceLivePresentation.Build(
            "card",
            leaseStatus,
            blockedReason: null,
            evidenceLogJson: null,
            appScreens: 0,
            featureFiles: 0,
            sharedFiles: 0,
            hasPackageJson: false,
            hasReadme: false,
            worktreeFileCount: 0,
            worktreeLastWriteUtc: null,
            filesCopiedThisTick: 0);

        return new WorkspaceLiveSnapshot(
            UpdatedAt: DateTimeOffset.UtcNow,
            TaskId: Guid.NewGuid(),
            LeaseId: Guid.NewGuid(),
            MirrorKeyId: Guid.NewGuid(),
            HermesSessionId: "h1",
            TaskTitle: "card",
            LeaseStatus: leaseStatus,
            BlockedReason: null,
            EvidenceLogJson: "{}",
            SessionWorkspaceRoot: "/workspace",
            LiveMirrorRoot: "/mirror",
            MirrorMode: "PerExecution",
            IsSharedSessionRootMirror: false,
            WorktreePath: "/wt",
            FilesCopiedThisTick: 0,
            AppScreenCount: 0,
            FeatureFileCount: 0,
            SharedFileCount: 0,
            HasPackageJson: false,
            HasReadme: false,
            WorktreeFileCount: 0,
            WorktreeLastWriteUtc: null,
            LastEvidenceKind: null,
            RecommendedPollSeconds: presentation.RecommendedPollSeconds,
            RecentEvidence: Array.Empty<string>(),
            LiveFilePath: "/mirror/live.md",
            Presentation: presentation);
    }

    private static ParallelWorkerMirrorEntry ShellWorker(string mergeState, string leaseStatus)
    {
        var worker = new ParallelWorkerMirrorEntry(
            TaskId: Guid.NewGuid(),
            TaskTitle: "card",
            ExecutionSessionId: Guid.NewGuid(),
            LeaseId: Guid.NewGuid(),
            HermesSessionId: "h1",
            LiveMirrorPath: "/mirror",
            LiveMarkdownPath: null,
            HealthState: "active",
            LifecycleStatus: "active",
            LastMirroredAt: DateTimeOffset.UtcNow,
            KanbanRevision: 1,
            KanbanPushedRevision: 1,
            KanbanStatus: "NeedsApproval",
            LeaseStatus: leaseStatus,
            WorktreePath: "/wt",
            IsSharedSessionRootMirror: false,
            RegistryCollision: null,
            MergeState: mergeState,
            MergeReadiness: null,
            MergeConflict: null,
            DecisionSummary: null!,
            ApproveGuardrails: null!,
            RevokeGuardrails: null!,
            RecommendedModeSlug: string.Empty,
            AvailableModeTransitions: Array.Empty<ModeTransitionHint>());

        var hints = OperationalModeNavigation.ForWorker(worker);
        return worker with
        {
            RecommendedModeSlug = hints.RecommendedModeSlug,
            AvailableModeTransitions = hints.AvailableTransitions,
        };
    }
}
