namespace JoyZoning.Domain.Orchestration;

/// <summary>
/// Distinct operator mental models in JoyZoning. Modes are intentionally separate —
/// do not collapse planning (kanban), execution (leases/workers), review (merge queue),
/// or habitat (ambient watch) into a single UI metaphor.
/// </summary>
public enum JoyZoningOperationalMode
{
    /// <summary>Backlog, cards, prioritization, assignment — Jira/Kanban intent.</summary>
    Planning = 0,

    /// <summary>Live workers, leases, Hermes sessions, mirrors, runtime observability.</summary>
    Execution = 1,

    /// <summary>Merge readiness, verification, conflicts, human approve/revoke — PR review.</summary>
    Review = 2,

    /// <summary>Optional atmosphere: pet/habitat/AFK — not the canonical ops surface.</summary>
    HabitatAmbient = 3,
}

public sealed record OperationalModeDescriptor(
    JoyZoningOperationalMode Mode,
    string Slug,
    string Title,
    string PrimaryQuestion,
    string ShortDescription,
    string MentalModel,
    string CanonicalMetaphor,
    bool IsCanonicalOperationalSurface,
    IReadOnlyList<string> CanonicalSurfaces,
    IReadOnlyList<string> ForbiddenInMode,
    IReadOnlyList<string> PrimaryEntities,
    IReadOnlyList<string> DesktopSurfaces,
    IReadOnlyList<string> WatchComponents,
    IReadOnlyList<string> ApiRouteHints);

/// <summary>Registry of mode boundaries for docs, UI routing, and read-model ownership.</summary>
public static class JoyZoningOperationalModes
{
    public static IReadOnlyList<OperationalModeDescriptor> All { get; } =
    [
        new(
            JoyZoningOperationalMode.Planning,
            Slug: "planning",
            Title: "Planning Mode",
            PrimaryQuestion: "What should happen?",
            ShortDescription: "Backlog, cards, prioritization, and assignment",
            MentalModel: "What should we do next? Who owns it? What is the backlog?",
            CanonicalMetaphor: "Jira / Kanban board",
            IsCanonicalOperationalSurface: true,
            CanonicalSurfaces: ["Kanban", "ManagerChat", "task picker", "session board"],
            ForbiddenInMode: ["approve_merge", "revoke_lease", "kanban_status_from_review_panel"],
            PrimaryEntities: ["WorkTask", "HermesKanbanTaskId", "KanbanRevision", "OperatorSession"],
            DesktopSurfaces: ["Kanban", "ManagerChat", "TaskCreate"],
            WatchComponents: ["KanbanBoard (session board)", "task picker / intake"],
            ApiRouteHints:
            [
                "GET/POST /api/tasks",
                "PUT /api/tasks/{id}/status",
                "POST /api/tasks/import-kanban",
                "KanbanSync / Hermes board",
            ]),
        new(
            JoyZoningOperationalMode.Execution,
            Slug: "execution",
            Title: "Execution Mode",
            PrimaryQuestion: "What are workers doing?",
            ShortDescription: "Live leases, Hermes sessions, mirrors, and runtime health",
            MentalModel: "What is running right now? Where is the worker? What is it doing?",
            CanonicalMetaphor: "Runtime orchestration / worker fleet",
            IsCanonicalOperationalSurface: true,
            CanonicalSurfaces: ["ExecutionViewport", "ParallelWorkersPanel", "live snapshot", "Timeline"],
            ForbiddenInMode: ["final_merge_approve", "final_merge_revoke", "kanban_backlog_edit"],
            PrimaryEntities:
            [
                "ExecutionLease",
                "ExecutionSession",
                "HermesSessionId",
                "WorktreePath",
                "WorkspaceLiveMirror",
            ],
            DesktopSurfaces: ["ExecutionViewport", "Timeline", "Workspace"],
            WatchComponents:
            [
                "ParallelWorkersPanel",
                "live task snapshot",
                "activity stream",
                "PetObservatory (technical)",
            ],
            ApiRouteHints:
            [
                "GET /api/tasks/{id}/live",
                "GET /api/sessions/{id}/parallel-workers",
                "POST /api/tasks/{id}/dispatch",
                "lease heartbeat / verify",
            ]),
        new(
            JoyZoningOperationalMode.Review,
            Slug: "review",
            Title: "Review Mode",
            PrimaryQuestion: "What can safely merge?",
            ShortDescription: "Merge queue, verification, conflicts, approve/revoke",
            MentalModel: "Is this safe to merge? What changed? What failed verification?",
            CanonicalMetaphor: "GitHub pull request review",
            IsCanonicalOperationalSurface: true,
            CanonicalSurfaces: ["MergeQueuePanel", "Workspace diffs", "decision preflight", "Approvals"],
            ForbiddenInMode: ["kanban_status_mutation", "dispatch_new_lease_from_review_only_view"],
            PrimaryEntities:
            [
                "WorkerMergeState",
                "VerificationReport",
                "OperatorDecisionSummary",
                "MergeConflictDetail",
            ],
            DesktopSurfaces: ["Approvals", "Workspace diffs", "Merge/Revoke actions"],
            WatchComponents:
            [
                "MergeQueuePanel",
                "WorkerDecisionConfirmDialog",
            ],
            ApiRouteHints:
            [
                "GET /api/sessions/{id}/merge-queue",
                "GET .../decision-preflight?action=accept|revoke|inspect",
                "POST /api/tasks/{id}/lease/merge",
                "POST /api/tasks/{id}/lease/revoke",
            ]),
        new(
            JoyZoningOperationalMode.HabitatAmbient,
            Slug: "habitat",
            Title: "Habitat / Ambient Mode",
            PrimaryQuestion: "What is the workspace atmosphere?",
            ShortDescription: "Pet mood, care meters, ambient presence — links into other modes",
            MentalModel: "How does the run feel? Is anything alarming at a glance?",
            CanonicalMetaphor: "Ambient observatory / emotional layer",
            IsCanonicalOperationalSurface: false,
            CanonicalSurfaces: ["SynthesisPet", "CareMeters", "ThoughtBubbles"],
            ForbiddenInMode:
            [
                "approve_merge",
                "revoke_lease",
                "kanban_mutation",
                "authoritative_merge_actions",
            ],
            PrimaryEntities: ["WorkspaceLivePresentation", "CareMeters", "PetMood"],
            DesktopSurfaces: [],
            WatchComponents:
            [
                "SynthesisPet",
                "CareMeters",
                "ThoughtBubbles",
                "HabitatShell / AFK layers",
            ],
            ApiRouteHints: ["GET /api/watch/bootstrap (presentation only)"]),
    ];

    public static OperationalModeDescriptor Get(JoyZoningOperationalMode mode) =>
        All.First(m => m.Mode == mode);

    public static OperationalModeDescriptor? TryGetBySlug(string? slug) =>
        All.FirstOrDefault(m => string.Equals(m.Slug, slug, StringComparison.OrdinalIgnoreCase));

    /// <summary>Maps a control-plane API path prefix to the owning mode (read-model hint).</summary>
    public static JoyZoningOperationalMode? InferFromApiPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var p = path.ToLowerInvariant();
        if (p.Contains("/merge-queue") || p.Contains("/decision-preflight") || p.Contains("/lease/merge")
            || p.Contains("/lease/revoke"))
            return JoyZoningOperationalMode.Review;

        if (p.Contains("/parallel-workers") || p.Contains("/live") || p.Contains("/dispatch")
            || p.Contains("/lease/heartbeat") || p.Contains("/lease/verify"))
            return JoyZoningOperationalMode.Execution;

        if (p.Contains("/tasks") || p.Contains("kanban"))
            return JoyZoningOperationalMode.Planning;

        if (p.Contains("/watch/"))
            return JoyZoningOperationalMode.HabitatAmbient;

        return null;
    }
}
