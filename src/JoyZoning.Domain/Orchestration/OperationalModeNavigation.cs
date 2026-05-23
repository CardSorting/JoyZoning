namespace JoyZoning.Domain.Orchestration;

/// <summary>Cross-mode handoff target for operator navigation.</summary>
public sealed record ModeTransitionHint(
    string TargetModeSlug,
    string Label,
    string Reason,
    string? HandoffKind = null);

public sealed record OperationalModeContextHints(
    string RecommendedModeSlug,
    IReadOnlyList<ModeTransitionHint> AvailableTransitions);

/// <summary>Recommends operational mode and transitions from lease/merge/read-model state.</summary>
public static class OperationalModeNavigation
{
    public const string SlugPlanning = "planning";
    public const string SlugExecution = "execution";
    public const string SlugReview = "review";
    public const string SlugHabitat = "habitat";

    public static OperationalModeContextHints ForWorker(ParallelWorkerMirrorEntry worker)
    {
        var recommended = RecommendModeSlugForWorker(worker);
        return new OperationalModeContextHints(recommended, BuildTransitionsForWorker(worker, recommended));
    }

    public static OperationalModeContextHints ForLiveTask(
        string? leaseStatus,
        string? mergeState = null,
        bool blocked = false)
    {
        var recommended = RecommendModeSlugForLive(leaseStatus, mergeState, blocked);
        return new OperationalModeContextHints(recommended, BuildTransitionsForLive(leaseStatus, mergeState, recommended));
    }

    public static string RecommendModeSlugForWorker(ParallelWorkerMirrorEntry worker)
    {
        if (worker.MergeState is "ready_to_merge" or "merge_conflict" or "merge_failed")
            return SlugReview;

        if (worker.MergeState is "merged" or "revoked")
            return SlugReview;

        if (worker.MergeState is "running" or "merging" or "stale")
            return SlugExecution;

        if (worker.LeaseStatus is "ReadyForReview" or "Verifying")
            return SlugReview;

        if (worker.LeaseStatus is "Running" or "Leased" or "Blocked")
            return SlugExecution;

        return SlugPlanning;
    }

    public static string RecommendModeSlugForLive(
        string? leaseStatus,
        string? mergeState,
        bool blocked)
    {
        if (!string.IsNullOrWhiteSpace(mergeState))
        {
            if (mergeState is "ready_to_merge" or "merge_conflict" or "merge_failed")
                return SlugReview;
        }

        if (string.IsNullOrWhiteSpace(leaseStatus))
            return SlugPlanning;

        if (leaseStatus is "ReadyForReview" or "Verifying")
            return SlugReview;

        if (leaseStatus is "Running" or "Leased" or "Blocked" || blocked)
            return SlugExecution;

        if (leaseStatus is "Merged" or "Revoked")
            return SlugReview;

        return SlugPlanning;
    }

    public static IReadOnlyList<ModeTransitionHint> BuildTransitionsForWorker(
        ParallelWorkerMirrorEntry worker,
        string? recommended = null)
    {
        recommended ??= RecommendModeSlugForWorker(worker);
        var transitions = new List<ModeTransitionHint>();

        AddIfNotCurrent(transitions, recommended, SlugPlanning, "View on board", "Kanban intent for this card", "planning_task");
        AddIfNotCurrent(transitions, recommended, SlugExecution, "Watch worker", "Live mirrors, lease health, activity", "execution_worker");

        var reviewReady = worker.MergeState is "ready_to_merge" or "merge_conflict" or "merge_failed"
            || worker.LeaseStatus is "ReadyForReview";
        if (reviewReady)
        {
            AddReviewTransition(transitions, recommended, "Review output", "Verification, merge, or conflicts", "review_worker");
        }

        AddIfNotCurrent(transitions, recommended, SlugHabitat, "Ambient glance", "Pet atmosphere — links only, not authoritative", "habitat_ambient");

        return transitions;
    }

    public static IReadOnlyList<ModeTransitionHint> BuildTransitionsForLive(
        string? leaseStatus,
        string? mergeState,
        string? recommended = null)
    {
        recommended ??= RecommendModeSlugForLive(leaseStatus, mergeState, blocked: false);
        var transitions = new List<ModeTransitionHint>();

        AddIfNotCurrent(transitions, recommended, SlugPlanning, "View on board", "Session kanban and task intent", "planning_task");
        AddIfNotCurrent(transitions, recommended, SlugExecution, "Watch execution", "What workers are doing right now", "execution_live");

        if (leaseStatus is "ReadyForReview" or "Verifying"
            || mergeState is "ready_to_merge" or "merge_conflict")
        {
            AddReviewTransition(transitions, recommended, "Review for merge", "Changed files and approve/revoke", "review_task");
        }

        AddIfNotCurrent(transitions, recommended, SlugHabitat, "Ambient glance", "Workspace atmosphere", "habitat_ambient");

        return transitions;
    }

    public static IReadOnlyList<ModeTransitionHint> StandardRegistryTransitions() =>
    [
        new(SlugPlanning, "Planning", "Backlog and kanban intent", "mode_registry"),
        new(SlugExecution, "Execution", "Worker orchestration", "mode_registry"),
        new(SlugReview, "Review", "Merge and verification", "mode_registry"),
        new(SlugHabitat, "Habitat", "Ambient layer", "mode_registry"),
    ];

    private static void AddIfNotCurrent(
        List<ModeTransitionHint> list,
        string current,
        string target,
        string label,
        string reason,
        string handoffKind)
    {
        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            return;

        list.Add(new ModeTransitionHint(target, label, reason, handoffKind));
    }

    /// <summary>Review handoffs stay visible even when review is already the recommended mode.</summary>
    private static void AddReviewTransition(
        List<ModeTransitionHint> list,
        string current,
        string label,
        string reason,
        string handoffKind)
    {
        if (list.Any(t => string.Equals(t.TargetModeSlug, SlugReview, StringComparison.OrdinalIgnoreCase)))
            return;

        var reviewLabel = string.Equals(current, SlugReview, StringComparison.OrdinalIgnoreCase)
            ? "Review queue"
            : label;
        list.Add(new ModeTransitionHint(SlugReview, reviewLabel, reason, handoffKind));
    }
}

/// <summary>UI/API guardrails — which actions are valid per mode.</summary>
public static class OperationalModeGuardrails
{
    public static bool AllowsKanbanMutation(string? modeSlug) =>
        string.Equals(modeSlug, OperationalModeNavigation.SlugPlanning, StringComparison.OrdinalIgnoreCase);

    public static bool AllowsMergeApproveRevoke(string? modeSlug) =>
        string.Equals(modeSlug, OperationalModeNavigation.SlugReview, StringComparison.OrdinalIgnoreCase);

    public static bool AllowsAuthoritativeWorkspaceActions(string? modeSlug) =>
        !string.Equals(modeSlug, OperationalModeNavigation.SlugHabitat, StringComparison.OrdinalIgnoreCase);

    public static bool ExecutionMayInspectOnly(string? modeSlug) =>
        string.Equals(modeSlug, OperationalModeNavigation.SlugExecution, StringComparison.OrdinalIgnoreCase);
}
