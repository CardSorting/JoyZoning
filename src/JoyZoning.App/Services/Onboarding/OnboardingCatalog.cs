namespace JoyZoning.App.Services.Onboarding;

/// <summary>
/// Product copy and step metadata — patterned after VS Code Welcome, Linear checklist, and Docker Desktop health panels.
/// </summary>
public static class OnboardingCatalog
{
    public static IReadOnlyList<OnboardingStepDefinition> CoreSteps { get; } =
    [
        new(
            OnboardingStepIds.ControlPlane,
            1,
            "Control plane online",
            "JoyZoning's local API on port 9470 orchestrates Hermes, sessions, and SignalR.",
            "Without the control plane, the desktop shell cannot reach agents or persist tasks.",
            "Retry",
            0,
            null,
            null),
        new(
            OnboardingStepIds.CliPaths,
            2,
            "diet-hermes ready",
            "One checkout at ~/Downloads/diet-hermes-main-master. Manager plans; executor runs — same Hermes, different sessions, kanban sync.",
            "JoyZoning does not install a second Hermes; roles split across sessions on the board.",
            "Set up diet-hermes",
            2,
            "https://github.com/NousResearch/hermes-agent",
            OnboardingSurfaceIds.GettingStarted),
        new(
            OnboardingStepIds.Gateway,
            3,
            "API gateway",
            "Hermes HTTP API (default :8642) for manager chat and task orchestration.",
            "Manager Chat and managed-run requests call into this gateway; it must be healthy before operators work.",
            "Start gateway",
            1,
            null,
            OnboardingSurfaceIds.GettingStarted),
        new(
            OnboardingStepIds.Dashboard,
            4,
            "Dashboard & session token",
            "Embedded TUI and kanban sync use the Hermes dashboard WebSocket token.",
            "JoyZoning acquires the token automatically — no copy/paste from browser devtools.",
            "Connect dashboard",
            2,
            null,
            OnboardingSurfaceIds.Execution),
        new(
            OnboardingStepIds.Workspace,
            5,
            "Open a workspace",
            "Bind JoyZoning to a project folder for sessions, kanban, and file review.",
            "Each workspace is an isolated operator session — same model as VS Code / Cursor folders.",
            "Open workspace",
            1,
            null,
            OnboardingSurfaceIds.Manager),
    ];

    public static IReadOnlyList<OnboardingStepDefinition> OptionalOperateSteps { get; } =
    [
        new(
            OnboardingStepIds.FirstChat,
            6,
            "Send a Manager Chat message",
            "Ask Hermes to plan work before requesting a supervised run from Kanban.",
            "Validates end-to-end LLM connectivity through your configured gateway.",
            "Open Manager Chat",
            2,
            null,
            OnboardingSurfaceIds.Manager,
            IsRequired: false),
        new(
            OnboardingStepIds.FirstDispatch,
            7,
            "Request first Hermes run",
            "Move a card to execution and observe the DietCode viewport.",
            "Confirms task pipeline + SignalR events are flowing.",
            "Open Kanban",
            2,
            null,
            OnboardingSurfaceIds.Kanban,
            IsRequired: false),
        new(
            OnboardingStepIds.TuiConnected,
            8,
            "Connect Hermes TUI",
            "Open the embedded terminal on Execution → Hermes TUI.",
            "Mirrors `hermes dashboard` PTY inside the cockpit — popular for power users.",
            "Connect TUI",
            2,
            null,
            OnboardingSurfaceIds.Execution,
            IsRequired: false),
    ];

    public static IReadOnlyList<SurfaceCoachMark> SurfaceCoachMarks { get; } =
    [
        new(OnboardingSurfaceIds.Manager,
            "Manager Chat",
            "Talk to Hermes like a tech lead: decompose goals, parse replies into tasks, then request a Hermes run from Kanban.",
            null),
        new(OnboardingSurfaceIds.Kanban,
            "Kanban board",
            "Import from Hermes, drag columns, and request a managed Hermes run. Status syncs back when connected.",
            null),
        new(OnboardingSurfaceIds.Execution,
            "Execution viewport",
            "Watch tool steps and terminal output. Use Hermes TUI for the full dashboard terminal experience.",
            null),
        new(OnboardingSurfaceIds.Workspace,
            "Workspace review",
            "Inspect changed files and split diffs after runs — operator-focused, not an IDE replacement.",
            null),
        new(OnboardingSurfaceIds.Timeline,
            "Event timeline",
            "Audit every operator action with JSON payloads. Useful for debugging sync and approvals.",
            null),
    ];

    public static IReadOnlyList<OnboardingWhatsNextCard> CompletionCards { get; } =
    [
        new("Plan with Hermes", "Open Manager Chat and describe what you want to ship.", OnboardingSurfaceIds.Manager, "Go to Manager Chat"),
        new("Sync kanban", "Import tasks from Hermes or create one and request a managed run.", OnboardingSurfaceIds.Kanban, "Go to Kanban"),
        new("Live TUI", "Connect the embedded Hermes terminal for interactive sessions.", OnboardingSurfaceIds.Execution, "Go to Execution"),
    ];

    /// <summary>Raycast / VS Code command-palette style shortcuts surfaced on the hub.</summary>
    public static IReadOnlyList<OnboardingQuickAction> QuickActions { get; } =
    [
        new("smart_setup", "Run smart setup", "Auto-detect path, start gateway, connect dashboard"),
        new("wizard", "Guided wizard", "Step-by-step first-run walkthrough"),
        new("connection", "Hermes connection", "Paths, tokens, test connection"),
        new("workspace", "Open workspace", "Bind a project folder"),
        new("tui", "Connect Hermes TUI", "Embedded dashboard terminal"),
    ];

    public static IReadOnlyList<OnboardingResourceLink> Resources { get; } =
    [
        new("Hermes Agent", "Upstream orchestration stack JoyZoning supervises.",
            "https://github.com/NousResearch/hermes-agent"),
        new("JoyZoning architecture", "Control plane, surfaces, and event model.",
            "docs/architecture.md", IsLocalDoc: true),
        new("Event catalog", "SignalR and REST events for debugging.",
            "docs/event-catalog.md", IsLocalDoc: true),
    ];

    public static IReadOnlyList<OnboardingPlaybook> TroubleshootingPlaybooks { get; } =
    [
        new(
            "Control plane offline",
            "JoyZoning cannot reach :9470.",
            [
                "Restart the app (it auto-spawns the control plane).",
                "Or run: dotnet run --project src/JoyZoning.ControlPlane",
                "Check nothing else is bound to port 9470.",
            ],
            OnboardingStepIds.ControlPlane),
        new(
            "hermes CLI not found",
            "Install path does not contain .venv/bin/hermes.",
            [
                "Use Auto-detect or Browse on Getting Started.",
                "Run `python -m venv .venv && pip install -e .` in diet-hermes.",
                "Confirm HERMES_INSTALL_ROOT env var if you use one.",
            ],
            OnboardingStepIds.CliPaths),
        new(
            "Dashboard token stale",
            "Dashboard runs but kanban/TUI cannot authenticate.",
            [
                "Getting Started → Connect dashboard, or Smart setup.",
                "Hermes → Connection → Refresh token.",
                "If you restarted `hermes dashboard`, reconnect once.",
            ],
            OnboardingStepIds.Dashboard),
    ];

    public static string PhaseLabel(OnboardingJourneyPhase phase) => phase switch
    {
        OnboardingJourneyPhase.NotStarted => "GETTING STARTED",
        OnboardingJourneyPhase.Configure => "CONFIGURE",
        OnboardingJourneyPhase.Connect => "CONNECT",
        OnboardingJourneyPhase.Operate => "OPERATE",
        OnboardingJourneyPhase.Complete => "READY",
        _ => "SETUP",
    };
}
