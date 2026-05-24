namespace JoyZoning.Domain.Orchestration;

public sealed record AgentCommandDescriptor(string Name, string Description, bool Safe);

/// <summary>Static Agent Operations manifest shared by CLI and control-plane HTTP.</summary>
public static class AgentOperationsManifest
{
    public const string ManifestVersion = "1";
    public const string GeneratedBy = "joyzoning agent-manifest";
    public const string AgentContractRelativePath = "docs/AGENT.md";

    public static readonly IReadOnlyList<string> CliBinaries = ["joyzoning", "jz"];

    public static readonly IReadOnlyList<string> ImportantFiles =
    [
        "src/JoyZoning.Cli/CliDispatcher.cs",
        "src/JoyZoning.Cli/JoyZoningCliClient.cs",
        "src/JoyZoning.Cli/AgentOperationsCommand.cs",
        "src/JoyZoning.Domain/Orchestration/AgentOperationsManifest.cs",
        "src/JoyZoning.Domain/Orchestration/AgentOperationsManifestCache.cs",
        "src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistry.cs",
        "src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistrySync.cs",
        "src/JoyZoning.ControlPlane/Endpoints/ApiEndpoints.cs",
        "src/JoyZoning.Domain/Orchestration/JoyZoningRuntimeContext.cs",
        "docs/AGENT.md",
        "docs/cli.md",
        "AGENTS.md",
    ];

    public static readonly IReadOnlyList<string> ProtectedPaths =
    [
        ".next/",
        "node_modules/",
        "generated/",
        "dist/",
        "bin/",
        "obj/",
    ];

    public static readonly IReadOnlyDictionary<string, string> VerificationCommands =
        new Dictionary<string, string>
        {
            ["typecheck"] = "dotnet build JoyZoning.sln --no-restore",
            ["tests"] = "./scripts/run-tests.sh fast",
            ["build"] = "dotnet build JoyZoning.sln",
            ["watchTypecheck"] = "npm run typecheck --prefix apps/joyzoning",
            ["watchTests"] = "npm run test --prefix apps/joyzoning",
        };

    public static readonly IReadOnlyList<string> FastVerificationKeys = ["typecheck", "build"];

    public static readonly IReadOnlyList<AgentCommandDescriptor> Commands =
    [
        new("status", "Summarize current workspace/session/control-plane state.", true),
        new("inspect", "Return compressed discovery hints for agents.", true),
        new("plan", "Create a task from a goal string or dry-run YOLO policy selection.", true),
        new("run", "Dispatch and run a task lease.", false),
        new("agent-manifest", "Return the canonical Agent Operations manifest.", true),
        new("agent-context", "Return minimal state an agent needs before acting.", true),
        new("endpoint-map", "Return the typed endpoint registry.", true),
        new("endpoints", "Return the typed endpoint registry as JSON or Markdown.", true),
        new("doctor", "Validate local agent-operation assumptions.", true),
        new("snapshot", "Return git, session, approval, and verification snapshot state.", true),
        new("task create", "Create work in the control plane.", true),
        new("task list", "List tasks for a session.", true),
        new("task read", "Read task-adjacent state through existing task and lease commands.", true),
        new("task run", "Dispatch and run a task lease.", false),
        new("task verify", "Run local verification and submit evidence.", false),
        new("verify", "Context-aware local verification shortcut.", false),
        new("verify --manifest", "Run manifest verification commands in workspace root.", true),
        new("verify --manifest --fast", "Run fast manifest verification (typecheck + build only).", true),
    ];

    public static readonly IReadOnlyDictionary<string, string> AgentWorkflow = new Dictionary<string, string>
    {
        ["beforeEdit"] = "joyzoning agent-context --json && joyzoning status --json",
        ["readScope"] = "Only files in importantFiles unless doctor reports stale manifest",
        ["afterEdit"] = "joyzoning verify --manifest --fast && joyzoning snapshot --json",
        ["orchestration"] = "joyzoning plan \"<goal>\" --session <guid> && joyzoning run <task-id>",
        ["httpFallback"] = "GET /api/agent/manifest, GET /api/agent/context, GET /api/agent/endpoints?agentSafe=true",
    };

    public static readonly IReadOnlyDictionary<string, string> HttpSurfaces = new Dictionary<string, string>
    {
        ["manifest"] = "/api/agent/manifest",
        ["context"] = "/api/agent/context",
        ["endpoints"] = "/api/agent/endpoints",
        ["endpointsAgentSafe"] = "/api/agent/endpoints?agentSafe=true",
        ["watchBootstrap"] = "/api/watch/bootstrap",
    };

    public static readonly IReadOnlyList<string> AvailableSurfaces =
    [
        "chat", "sessions", "workers", "approvals", "verification", "workspace", "endpoints", "agent-manifest",
    ];

    public static readonly IReadOnlyList<string> WorkspaceAssumptions =
    [
        "The canonical CLI binary may be installed as joyzoning or jz.",
        "Agents stop at ReadyForReview; humans own merge and Complete.",
        "The endpoint registry is authoritative for agent-safe API discovery.",
        "Use doctor before scanning the repo when manifest assumptions look stale.",
        "HTTP /api/agent/manifest and /api/agent/context mirror agent operations when CLI is unavailable.",
        "Cached manifest lives at .joyzoning/agent-manifest.json after agent-manifest or passing doctor.",
    ];

    public static readonly IReadOnlyList<string> Entrypoints =
    [
        "joyzoning agent-context --json",
        "joyzoning agent-manifest --json",
        "joyzoning endpoints --json",
        "joyzoning doctor --json",
    ];

    public static object BuildStatic() => new
    {
        manifestVersion = ManifestVersion,
        fingerprint = AgentOperationsManifestCache.ComputeStaticFingerprint(),
        app = "joyzoning",
        generatedBy = GeneratedBy,
        cliBinaries = CliBinaries,
        commands = Commands,
        workflow = AgentWorkflow,
        endpoints = JoyZoningEndpointRegistry.Endpoints,
        agentSafeEndpoints = JoyZoningEndpointRegistry.Endpoints.Where(e => e.AgentSafe).ToList(),
        endpointSummary = BuildEndpointSummary(syncedWithApi: null),
        verification = VerificationCommands,
        verificationTiers = new
        {
            fast = FastVerificationKeys,
            full = VerificationCommands.Keys,
        },
        availableSurfaces = AvailableSurfaces,
        importantFiles = ImportantFiles,
        protectedPaths = ProtectedPaths,
        assumptions = WorkspaceAssumptions,
        entrypoints = Entrypoints,
        agentContract = AgentContractRelativePath,
        manifestCache = AgentOperationsManifestCache.RelativePath,
        http = HttpSurfaces,
    };

    public static object BuildEndpointSummary(bool? syncedWithApi)
    {
        var agentSafe = JoyZoningEndpointRegistry.Endpoints.Count(e => e.AgentSafe);
        return new
        {
            total = JoyZoningEndpointRegistry.Endpoints.Count,
            agentSafe,
            operatorOnly = JoyZoningEndpointRegistry.Endpoints.Count - agentSafe,
            syncedWithApi,
        };
    }

    public static IEnumerable<KeyValuePair<string, string>> ResolveVerificationCommands(bool fast) =>
        fast
            ? VerificationCommands.Where(kv => FastVerificationKeys.Contains(kv.Key))
            : VerificationCommands;
}
