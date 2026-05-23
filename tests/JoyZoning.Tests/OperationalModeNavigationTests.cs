using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class OperationalModeNavigationTests
{
    [Fact]
    public void Registry_exposes_primary_question_for_all_four_modes()
    {
        Assert.Equal(4, JoyZoningOperationalModes.All.Count);
        Assert.All(JoyZoningOperationalModes.All, m => Assert.False(string.IsNullOrWhiteSpace(m.PrimaryQuestion)));

        Assert.Equal("What should happen?", JoyZoningOperationalModes.Get(JoyZoningOperationalMode.Planning).PrimaryQuestion);
        Assert.Equal("What are workers doing?", JoyZoningOperationalModes.Get(JoyZoningOperationalMode.Execution).PrimaryQuestion);
        Assert.Equal("What can safely merge?", JoyZoningOperationalModes.Get(JoyZoningOperationalMode.Review).PrimaryQuestion);
        Assert.Equal(
            "What is the workspace atmosphere?",
            JoyZoningOperationalModes.Get(JoyZoningOperationalMode.HabitatAmbient).PrimaryQuestion);
    }

    [Fact]
    public void Planning_forbids_merge_actions_in_registry()
    {
        var planning = JoyZoningOperationalModes.Get(JoyZoningOperationalMode.Planning);
        Assert.Contains(planning.ForbiddenInMode, f => f.Contains("approve_merge", StringComparison.Ordinal));
        Assert.False(OperationalModeGuardrails.AllowsMergeApproveRevoke(planning.Slug));
    }

    [Fact]
    public void Review_forbids_kanban_mutation_in_registry()
    {
        var review = JoyZoningOperationalModes.Get(JoyZoningOperationalMode.Review);
        Assert.Contains(review.ForbiddenInMode, f => f.Contains("kanban", StringComparison.OrdinalIgnoreCase));
        Assert.False(OperationalModeGuardrails.AllowsKanbanMutation(review.Slug));
    }

    [Fact]
    public void Habitat_is_non_canonical_and_forbids_authoritative_actions()
    {
        var habitat = JoyZoningOperationalModes.Get(JoyZoningOperationalMode.HabitatAmbient);
        Assert.False(habitat.IsCanonicalOperationalSurface);
        Assert.False(OperationalModeGuardrails.AllowsAuthoritativeWorkspaceActions(habitat.Slug));
        Assert.Contains(habitat.ForbiddenInMode, f => f.Contains("approve_merge", StringComparison.Ordinal));
    }

    [Fact]
    public void Ready_to_merge_worker_recommends_review_mode()
    {
        var worker = ShellWorker(mergeState: "ready_to_merge", leaseStatus: "ReadyForReview");
        var hints = OperationalModeNavigation.ForWorker(worker);
        Assert.Equal(OperationalModeNavigation.SlugReview, hints.RecommendedModeSlug);
    }

    [Fact]
    public void Running_worker_recommends_execution_mode()
    {
        var worker = ShellWorker(mergeState: "running", leaseStatus: "Running");
        var hints = OperationalModeNavigation.ForWorker(worker);
        Assert.Equal(OperationalModeNavigation.SlugExecution, hints.RecommendedModeSlug);
    }

    [Fact]
    public void Live_ready_for_review_recommends_review()
    {
        var hints = OperationalModeNavigation.ForLiveTask("ReadyForReview");
        Assert.Equal(OperationalModeNavigation.SlugReview, hints.RecommendedModeSlug);
        Assert.Contains(hints.AvailableTransitions, t => t.TargetModeSlug == OperationalModeNavigation.SlugReview);
    }

    [Fact]
    public void Worker_includes_transition_to_review_when_ready()
    {
        var worker = ShellWorker(mergeState: "ready_to_merge", leaseStatus: "ReadyForReview");
        var hints = OperationalModeNavigation.ForWorker(worker);
        Assert.Contains(
            hints.AvailableTransitions,
            t => t.TargetModeSlug == OperationalModeNavigation.SlugExecution);
        Assert.Contains(
            hints.AvailableTransitions,
            t => t.TargetModeSlug == OperationalModeNavigation.SlugReview);
    }

    [Fact]
    public void Registry_transitions_cover_all_four_modes()
    {
        var transitions = OperationalModeNavigation.StandardRegistryTransitions();
        Assert.NotEmpty(transitions);
        Assert.Contains(transitions, t => t.TargetModeSlug == OperationalModeNavigation.SlugPlanning);
        Assert.Contains(transitions, t => t.TargetModeSlug == OperationalModeNavigation.SlugExecution);
        Assert.Contains(transitions, t => t.TargetModeSlug == OperationalModeNavigation.SlugReview);
        Assert.Contains(transitions, t => t.TargetModeSlug == OperationalModeNavigation.SlugHabitat);
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
