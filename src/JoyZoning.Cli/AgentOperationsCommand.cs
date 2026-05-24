using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class AgentOperationsCommand
{
    public static readonly IReadOnlyList<string> ImportantFiles =
    [
        "src/JoyZoning.Cli/CliDispatcher.cs",
        "src/JoyZoning.Cli/JoyZoningCliClient.cs",
        "src/JoyZoning.Cli/AgentOperationsCommand.cs",
        "src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistry.cs",
        "src/JoyZoning.ControlPlane/Endpoints/ApiEndpoints.cs",
        "src/JoyZoning.Domain/Orchestration/JoyZoningRuntimeContext.cs",
        "docs/AGENT.md",
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

    private static readonly IReadOnlyList<object> Commands =
    [
        new { name = "status", description = "Summarize current workspace/session/control-plane state.", safe = true },
        new { name = "inspect", description = "Return compressed discovery hints for agents.", safe = true },
        new { name = "agent-manifest", description = "Return the canonical Agent Operations manifest.", safe = true },
        new { name = "agent-context", description = "Return minimal state an agent needs before acting.", safe = true },
        new { name = "endpoint-map", description = "Return the typed endpoint registry.", safe = true },
        new { name = "endpoints", description = "Return the typed endpoint registry as JSON or Markdown.", safe = true },
        new { name = "doctor", description = "Validate local agent-operation assumptions.", safe = true },
        new { name = "snapshot", description = "Return git, session, approval, and verification snapshot state.", safe = true },
        new { name = "task create", description = "Create work in the control plane.", safe = true },
        new { name = "task list", description = "List tasks for a session.", safe = true },
        new { name = "task read", description = "Read task-adjacent state through existing task and lease commands.", safe = true },
        new { name = "task run", description = "Dispatch and run a task lease.", safe = false },
        new { name = "task verify", description = "Run local verification and submit evidence.", safe = false },
        new { name = "verify", description = "Context-aware local verification shortcut.", safe = false },
    ];

    public static async Task<int> DispatchAsync(JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        var cmd = a[0].ToLowerInvariant();
        return cmd switch
        {
            "agent-manifest" => CliOutput.WriteEnvelope(ctx, await BuildManifestAsync(ctx)),
            "agent-context" => CliOutput.WriteEnvelope(ctx, await BuildAgentContextAsync(ctx)),
            "inspect" => CliOutput.WriteEnvelope(ctx, BuildInspect(FindWorkspaceRoot())),
            "status" => CliOutput.WriteEnvelope(ctx, await BuildStatusAsync(ctx)),
            "snapshot" => CliOutput.WriteEnvelope(ctx, await BuildSnapshotAsync(ctx)),
            "endpoints" or "endpoint-map" => WriteEndpoints(ctx),
            _ => throw new CliUsageException($"Unknown agent operations command: {cmd}."),
        };
    }

    public static object BuildInspect(string root) => new
    {
        project = "JoyZoning",
        mode = "watch-app",
        availableSurfaces = new[] { "chat", "sessions", "workers", "approvals", "verification", "workspace", "endpoints" },
        importantFiles = ImportantFiles,
        doNotEdit = ProtectedPaths,
        endpointRegistry = "src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistry.cs",
        agentContract = "docs/AGENT.md",
        workspaceRoot = root,
    };

    public static async Task<object> BuildManifestAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var local = BuildLocalDoctor(root);
        var runtime = JoyZoningRuntimeContext.TryLoad();
        var health = await ProbeHealthAsync(ctx.BaseUrl);
        return new
        {
            app = "joyzoning",
            version = ResolveAppVersion(root),
            workspace = new
            {
                root = ".",
                absoluteRoot = root,
                activeSession = ResolveActiveSession(runtime, ctx.Args),
                health = local.Ok && health.Status == "ok" ? "ok" : "degraded",
                assumptions = WorkspaceAssumptions(),
            },
            commands = Commands,
            endpoints = JoyZoningEndpointRegistry.Endpoints,
            verification = VerificationCommands,
            availableSurfaces = new[] { "chat", "sessions", "workers", "approvals", "verification", "workspace", "endpoints" },
            importantFiles = ImportantFiles,
            protectedPaths = ProtectedPaths,
            generatedBy = "joyzoning agent-manifest",
            doctor = new
            {
                ok = local.Ok,
                checks = local.Checks,
            },
        };
    }

    public static async Task<object> BuildAgentContextAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var runtime = JoyZoningRuntimeContext.TryLoad();
        var git = ReadGitState(root);
        var health = await ProbeHealthAsync(ctx.BaseUrl);
        var activeSession = ResolveActiveSession(runtime, ctx.Args);
        var approvals = health.Status == "ok"
            ? await TryCountAsync(ctx.BaseUrl, "approvals")
            : UnavailableCount("control plane unavailable");
        var tasks = health.Status == "ok" && activeSession is not null
            ? await TryCountAsync(ctx.BaseUrl, "tasks", activeSession)
            : UnavailableCount(activeSession is null ? "no active session" : "control plane unavailable");

        return new
        {
            app = "joyzoning",
            workspaceRoot = root,
            activeSession,
            activeTask = runtime?.TaskId,
            git,
            health,
            pendingApprovals = approvals,
            activeTasks = tasks,
            recentVerification = runtime?.LastVerification is null
                ? null
                : new
                {
                    runtime.LastVerification.At,
                    passed = runtime.LastVerification.AllPassed,
                    runtime.LastVerification.Commands,
                },
            importantFiles = ImportantFiles,
            protectedPaths = ProtectedPaths,
            nextCommands = new[]
            {
                "joyzoning status --json",
                "joyzoning endpoints --json",
                "joyzoning doctor --json",
            },
        };
    }

    public static async Task<object> BuildStatusAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var runtime = JoyZoningRuntimeContext.TryLoad();
        var health = await ProbeHealthAsync(ctx.BaseUrl);
        return new
        {
            app = "joyzoning",
            workspace = new
            {
                root,
                activeSession = ResolveActiveSession(runtime, ctx.Args),
                activeTask = runtime?.TaskId,
                health = health.Status,
                controlPlane = health,
            },
            git = ReadGitState(root),
            verification = runtime?.LastVerification,
            surfaces = new[] { "sessions", "tasks", "leases", "approvals", "events", "workspace", "hermes" },
        };
    }

    public static async Task<object> BuildSnapshotAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var runtime = JoyZoningRuntimeContext.TryLoad();
        var activeSession = ResolveActiveSession(runtime, ctx.Args);
        var health = await ProbeHealthAsync(ctx.BaseUrl);
        return new
        {
            git = ReadGitState(root),
            tests = new
            {
                lastRun = runtime?.LastVerification is null
                    ? "unknown"
                    : runtime.LastVerification.AllPassed ? "passed" : "failed",
                failedFiles = Array.Empty<string>(),
                commands = runtime?.LastVerification?.Commands ?? Array.Empty<string>(),
            },
            sessions = new
            {
                active = activeSession is null ? 0 : 1,
                activeSession,
                blocked = 0,
            },
            approvals = health.Status == "ok"
                ? await TryCountAsync(ctx.BaseUrl, "approvals")
                : UnavailableCount("control plane unavailable"),
        };
    }

    public static DoctorReport BuildLocalDoctor(string root)
    {
        var checks = new List<DoctorCheck>();

        void Check(string id, bool ok, string detail, string failStatus = "fail")
        {
            checks.Add(new DoctorCheck(id, ok ? "ok" : failStatus, detail));
        }

        Check("workspace_root", File.Exists(Path.Combine(root, "JoyZoning.sln")),
            "JoyZoning.sln is present at workspace root.");
        Check("agent_contract", File.Exists(Path.Combine(root, "docs/AGENT.md")),
            "docs/AGENT.md exists and is available to agents.");
        Check("endpoint_registry", JoyZoningEndpointRegistry.Endpoints.Count > 0 &&
                                   JoyZoningEndpointRegistry.Endpoints.Any(e => e.Id == "create-session"),
            $"Endpoint registry loaded {JoyZoningEndpointRegistry.Endpoints.Count} endpoints.");
        Check("session_endpoints", JoyZoningEndpointRegistry.Endpoints.Any(e => e.Method == "POST" && e.Path == "/api/sessions"),
            "Registry includes POST /api/sessions.");
        Check("protected_paths", ProtectedPaths.Contains(".next/") &&
                                 ProtectedPaths.Contains("node_modules/") &&
                                 ProtectedPaths.Contains("generated/"),
            "Protected paths include .next/, node_modules/, and generated/.");

        var missingImportant = ImportantFiles
            .Where(path => !File.Exists(Path.Combine(root, path)))
            .ToList();
        Check("required_files", missingImportant.Count == 0,
            missingImportant.Count == 0
                ? "All agent-operation important files exist."
                : "Missing: " + string.Join(", ", missingImportant));

        Check("test_script", File.Exists(Path.Combine(root, "scripts/run-tests.sh")),
            "scripts/run-tests.sh exists.");
        Check("cli_script", File.Exists(Path.Combine(root, "scripts/jz")),
            "scripts/jz exists.");
        Check("manifest_fresh", File.Exists(Path.Combine(root, "docs/AGENT.md")) &&
                                File.ReadAllText(Path.Combine(root, "docs/AGENT.md"))
                                    .Contains("joyzoning agent-context --json", StringComparison.Ordinal),
            "Agent contract references the canonical agent-context command.");
        Check("watch_package_scripts", WatchPackageHasScripts(root),
            "apps/joyzoning/package.json has build, typecheck, and test scripts.", "warn");

        return new DoctorReport(!checks.Any(c => c.Status == "fail"), checks);
    }

    public static string FindWorkspaceRoot(string? start = null)
    {
        var dir = new DirectoryInfo(start ?? Environment.CurrentDirectory);
        for (var current = dir; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "JoyZoning.sln")))
                return current.FullName;
        }

        return dir.FullName;
    }

    private static int WriteEndpoints(CliContext ctx)
    {
        if (ctx.Args.Has("--markdown"))
        {
            Console.Out.WriteLine(BuildEndpointMarkdown());
            return 0;
        }

        return CliOutput.WriteEnvelope(ctx, new
        {
            endpoints = JoyZoningEndpointRegistry.Endpoints,
        });
    }

    private static string BuildEndpointMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# JoyZoning Endpoint Map");
        sb.AppendLine();
        sb.AppendLine("| ID | Method | Path | Agent-safe | Purpose |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var e in JoyZoningEndpointRegistry.Endpoints)
            sb.AppendLine($"| {e.Id} | {e.Method} | `{e.Path}` | {e.AgentSafe.ToString().ToLowerInvariant()} | {e.Purpose} |");
        return sb.ToString();
    }

    private static async Task<HealthProbe> ProbeHealthAsync(string baseUrl)
    {
        using var client = new JoyZoningCliClient(baseUrl, TimeSpan.FromSeconds(3));
        var health = await client.HealthAsync();
        return health.IsSuccess
            ? new HealthProbe("ok", baseUrl, null)
            : new HealthProbe("unavailable", baseUrl, health.Message ?? health.Error ?? health.RawText);
    }

    private static async Task<object> TryCountAsync(string baseUrl, string kind, Guid? sessionId = null)
    {
        using var client = new JoyZoningCliClient(baseUrl, TimeSpan.FromSeconds(3));
        var result = kind switch
        {
            "approvals" => await client.ListPendingApprovalsAsync(),
            "tasks" when sessionId.HasValue => await client.ListTasksAsync(sessionId.Value),
            _ => CliHttpResult.NetworkError("unsupported count target"),
        };

        if (!result.IsSuccess || result.Body is null)
            return UnavailableCount(result.Message ?? result.Error ?? "request failed");

        return new
        {
            count = CountJsonItems(result.Body.Value),
            available = true,
        };
    }

    private static object UnavailableCount(string reason) => new
    {
        count = (int?)null,
        available = false,
        reason,
    };

    private static int? CountJsonItems(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.GetArrayLength();
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "items", "tasks", "approvals", "sessions", "events" })
            {
                if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
                    return prop.GetArrayLength();
            }
        }

        return null;
    }

    private static object ReadGitState(string root)
    {
        var branch = RunProcess(root, "git", "rev-parse --abbrev-ref HEAD");
        var status = RunProcess(root, "git", "status --porcelain");
        return new
        {
            branch = branch.ExitCode == 0 ? branch.Stdout.Trim() : "unknown",
            dirty = status.ExitCode == 0 && !string.IsNullOrWhiteSpace(status.Stdout),
            available = branch.ExitCode == 0 && status.ExitCode == 0,
        };
    }

    private static (int ExitCode, string Stdout, string Stderr) RunProcess(string workingDirectory, string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return (-1, "", "process did not start");

            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { /* ignore */ }
                return (-1, "", "process timed out");
            }

            return (process.ExitCode, process.StandardOutput.ReadToEnd(), process.StandardError.ReadToEnd());
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    private static Guid? ResolveActiveSession(JoyZoningRuntimeContext? runtime, CliArgs args) =>
        runtime?.SessionId ?? args.SessionId;

    private static IReadOnlyList<string> WorkspaceAssumptions() =>
    [
        "The canonical CLI binary may be installed as joyzoning or jz.",
        "Agents stop at ReadyForReview; humans own merge and Complete.",
        "The endpoint registry is authoritative for agent-safe API discovery.",
        "Use doctor before scanning the repo when manifest assumptions look stale.",
    ];

    private static string ResolveAppVersion(string root)
    {
        foreach (var path in new[] { "apps/joyzoning/package.json", "package.json" })
        {
            var full = Path.Combine(root, path);
            if (!File.Exists(full))
                continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(full));
                if (doc.RootElement.TryGetProperty("version", out var version))
                    return version.GetString() ?? "0.1.0";
            }
            catch
            {
                return "0.1.0";
            }
        }

        return "0.1.0";
    }

    private static bool WatchPackageHasScripts(string root)
    {
        var full = Path.Combine(root, "apps/joyzoning/package.json");
        if (!File.Exists(full))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(full));
            if (!doc.RootElement.TryGetProperty("scripts", out var scripts))
                return false;

            return scripts.TryGetProperty("build", out _) &&
                   scripts.TryGetProperty("typecheck", out _) &&
                   scripts.TryGetProperty("test", out _);
        }
        catch
        {
            return false;
        }
    }
}

public sealed record DoctorReport(bool Ok, IReadOnlyList<DoctorCheck> Checks);

public sealed record DoctorCheck(string Id, string Status, string Detail);

public sealed record HealthProbe(string Status, string BaseUrl, string? Message);
