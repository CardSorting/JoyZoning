using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class AgentOperationsCommand
{
    public static string ManifestVersion => AgentOperationsManifest.ManifestVersion;

    public static IReadOnlyList<string> ImportantFiles => AgentOperationsManifest.ImportantFiles;
    public static IReadOnlyList<string> ProtectedPaths => AgentOperationsManifest.ProtectedPaths;
    public static IReadOnlyDictionary<string, string> VerificationCommands => AgentOperationsManifest.VerificationCommands;
    public static IReadOnlyDictionary<string, string> AgentWorkflow => AgentOperationsManifest.AgentWorkflow;

    public static async Task<int> DispatchAsync(JoyZoningCliClient client, CliContext ctx, string[] a)
    {
        var cmd = a[0].ToLowerInvariant();
        return cmd switch
        {
            "agent-manifest" => await WriteManifestAsync(client, ctx),
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
        manifestVersion = ManifestVersion,
        project = "JoyZoning",
        mode = "watch-app",
        cliBinaries = AgentOperationsManifest.CliBinaries,
        entrypoints = AgentOperationsManifest.Entrypoints,
        availableSurfaces = AgentOperationsManifest.AvailableSurfaces,
        importantFiles = ImportantFiles,
        doNotEdit = ProtectedPaths,
        endpointRegistry = JoyZoningEndpointRegistrySync.ApiEndpointsRelativePath,
        endpointRegistryType = "src/JoyZoning.Domain/Orchestration/JoyZoningEndpointRegistry.cs",
        agentContract = AgentOperationsManifest.AgentContractRelativePath,
        agentsEntry = "AGENTS.md",
        workspaceRoot = root,
        endpointSummary = BuildEndpointSummary(root),
        manifestCache = AgentOperationsManifestCache.RelativePath,
        http = AgentOperationsManifest.HttpSurfaces,
    };

    private static async Task<int> WriteManifestAsync(JoyZoningCliClient client, CliContext ctx)
    {
        var manifest = await BuildManifestAsync(ctx);
        TryWriteManifestCache(FindWorkspaceRoot(), manifest);
        return CliOutput.WriteEnvelope(ctx, manifest);
    }

    public static void TryWriteManifestCache(string root, object manifest)
    {
        try
        {
            AgentOperationsManifestCache.Write(root, new
            {
                fingerprint = AgentOperationsManifestCache.ComputeWorkspaceFingerprint(root),
                importantFilesHash = AgentOperationsManifestCache.HashImportantFiles(root),
                generatedAt = DateTimeOffset.UtcNow,
                manifest,
            });
        }
        catch
        {
            // Best-effort cache for offline agents.
        }
    }

    public static async Task<object> BuildManifestAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var local = BuildLocalDoctor(root);
        var runtime = JoyZoningRuntimeContext.TryLoad();
        var health = await ProbeHealthAsync(ctx.BaseUrl);
        var sync = JoyZoningEndpointRegistrySync.CompareRegistryToApiFile(root);
        return new
        {
            manifestVersion = ManifestVersion,
            fingerprint = AgentOperationsManifestCache.ComputeWorkspaceFingerprint(root),
            importantFilesHash = AgentOperationsManifestCache.HashImportantFiles(root),
            app = "joyzoning",
            version = ResolveAppVersion(root),
            generatedAt = DateTimeOffset.UtcNow,
            cliBinaries = AgentOperationsManifest.CliBinaries,
            workspace = new
            {
                root = ".",
                absoluteRoot = root,
                activeSession = ResolveActiveSession(runtime, ctx.Args),
                health = local.Ok && health.Status == "ok" ? "ok" : "degraded",
                assumptions = AgentOperationsManifest.WorkspaceAssumptions,
            },
            commands = AgentOperationsManifest.Commands,
            workflow = AgentWorkflow,
            endpoints = JoyZoningEndpointRegistry.Endpoints,
            agentSafeEndpoints = JoyZoningEndpointRegistry.Endpoints.Where(e => e.AgentSafe).ToList(),
            endpointSummary = BuildEndpointSummary(root, sync),
            verification = VerificationCommands,
            verificationTiers = new
            {
                fast = AgentOperationsManifest.FastVerificationKeys,
                full = VerificationCommands.Keys,
            },
            availableSurfaces = AgentOperationsManifest.AvailableSurfaces,
            importantFiles = ImportantFiles,
            protectedPaths = ProtectedPaths,
            generatedBy = AgentOperationsManifest.GeneratedBy,
            httpManifest = $"{ctx.BaseUrl.TrimEnd('/')}/api/agent/manifest",
            httpContext = $"{ctx.BaseUrl.TrimEnd('/')}/api/agent/context",
            manifestCache = AgentOperationsManifestCache.RelativePath,
            http = AgentOperationsManifest.HttpSurfaces,
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
            ? await TrySessionTaskSummaryAsync(ctx.BaseUrl, activeSession.Value)
            : UnavailableTaskSummary(activeSession is null ? "no active session" : "control plane unavailable");

        return new
        {
            manifestVersion = ManifestVersion,
            app = "joyzoning",
            workspaceRoot = root,
            activeSession,
            activeTask = runtime?.TaskId,
            activeLease = runtime?.LeaseId,
            leaseStatus = runtime?.LeaseStatus,
            git,
            health,
            pendingApprovals = approvals,
            activeTasks = tasks,
            endpointSummary = BuildEndpointSummary(root),
            contract = AgentOperationsManifest.AgentContractRelativePath,
            agentsEntry = "AGENTS.md",
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
            nextCommands = AgentOperationsManifest.Entrypoints,
        };
    }

    public static async Task<object> BuildStatusAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var runtime = JoyZoningRuntimeContext.TryLoad();
        var health = await ProbeHealthAsync(ctx.BaseUrl);
        return new
        {
            manifestVersion = ManifestVersion,
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
            surfaces = new[] { "sessions", "tasks", "leases", "approvals", "events", "workspace", "hermes", "agent-manifest" },
            endpointSummary = BuildEndpointSummary(root),
        };
    }

    public static async Task<object> BuildSnapshotAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var runtime = JoyZoningRuntimeContext.TryLoad();
        var activeSession = ResolveActiveSession(runtime, ctx.Args);
        var health = await ProbeHealthAsync(ctx.BaseUrl);
        var localDoctor = BuildLocalDoctor(root);
        var taskSummary = health.Status == "ok" && activeSession is not null
            ? await TrySessionTaskSummaryAsync(ctx.BaseUrl, activeSession.Value)
            : UnavailableTaskSummary(activeSession is null ? "no active session" : "control plane unavailable");

        return new
        {
            manifestVersion = ManifestVersion,
            git = ReadGitState(root),
            tests = new
            {
                lastRun = runtime?.LastVerification is null
                    ? "unknown"
                    : runtime.LastVerification.AllPassed ? "passed" : "failed",
                failedFiles = Array.Empty<string>(),
                commands = runtime?.LastVerification?.Commands ?? Array.Empty<string>(),
                manifestCommands = VerificationCommands.Values,
            },
            sessions = new
            {
                active = activeSession is null ? 0 : 1,
                activeSession,
                blocked = ReadBlockedCount(taskSummary),
            },
            tasks = taskSummary,
            approvals = health.Status == "ok"
                ? await TryCountAsync(ctx.BaseUrl, "approvals")
                : UnavailableCount("control plane unavailable"),
            endpointSummary = BuildEndpointSummary(root),
            manifestFresh = localDoctor.Ok,
            doctorOk = localDoctor.Ok,
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
        Check("agent_contract", File.Exists(Path.Combine(root, AgentOperationsManifest.AgentContractRelativePath)),
            "docs/AGENT.md exists and is available to agents.");
        Check("agents_entry", File.Exists(Path.Combine(root, "AGENTS.md")),
            "AGENTS.md exists at repo root for Cursor/agent entry.");
        Check("endpoint_registry", JoyZoningEndpointRegistry.Endpoints.Count > 0 &&
                                   JoyZoningEndpointRegistry.Endpoints.Any(e => e.Id == "create-session"),
            $"Endpoint registry loaded {JoyZoningEndpointRegistry.Endpoints.Count} endpoints.");
        var sync = JoyZoningEndpointRegistrySync.CompareRegistryToApiFile(root);
        Check("endpoint_registry_sync", sync.Ok, JoyZoningEndpointRegistrySync.FormatDiff(sync));
        Check("session_endpoints", JoyZoningEndpointRegistry.Endpoints.Any(e => e.Method == "POST" && e.Path == "/api/sessions"),
            "Registry includes POST /api/sessions.");
        Check("agent_http_manifest", JoyZoningEndpointRegistry.Endpoints.Any(e => e.Path == "/api/agent/manifest"),
            "Registry includes GET /api/agent/manifest for HTTP fallback.");
        Check("agent_http_context", JoyZoningEndpointRegistry.Endpoints.Any(e => e.Path == "/api/agent/context"),
            "Registry includes GET /api/agent/context for HTTP runtime state.");
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
        Check("joyzoning_script", File.Exists(Path.Combine(root, "scripts/joyzoning")),
            "scripts/joyzoning exists for the canonical agent CLI name.");
        Check("cli_publish", IsCliPublishFresh(root),
            IsCliPublishFresh(root)
                ? "dist/jz-publish/jz is present and not older than CLI sources."
                : "Rebuild CLI: dotnet build src/JoyZoning.Cli/JoyZoning.Cli.csproj -o dist/jz-publish",
            "warn");
        Check("manifest_fresh", File.Exists(Path.Combine(root, AgentOperationsManifest.AgentContractRelativePath)) &&
                                File.ReadAllText(Path.Combine(root, AgentOperationsManifest.AgentContractRelativePath))
                                    .Contains("joyzoning agent-context --json", StringComparison.Ordinal) &&
                                File.ReadAllText(Path.Combine(root, AgentOperationsManifest.AgentContractRelativePath))
                                    .Contains("manifestVersion", StringComparison.Ordinal),
            "Agent contract references canonical commands and manifest version.");
        var expectedFingerprint = AgentOperationsManifestCache.ComputeWorkspaceFingerprint(root);
        var cacheCurrent = AgentOperationsManifestCache.TryReadFingerprint(root, out var cachedFingerprint) &&
                           cachedFingerprint == expectedFingerprint;
        Check("manifest_cache", cacheCurrent,
            cacheCurrent
                ? $"Cached manifest at {AgentOperationsManifestCache.RelativePath} matches current fingerprint."
                : $"Refresh cache: joyzoning agent-manifest --json (writes {AgentOperationsManifestCache.RelativePath}).",
            "warn");
        Check("watch_package_scripts", WatchPackageHasScripts(root),
            "apps/joyzoning/package.json has build, typecheck, and test scripts.", "warn");

        return new DoctorReport(!checks.Any(c => c.Status == "fail"), checks);
    }

    public static Task<int> RunManifestVerificationAsync(CliContext ctx)
    {
        var root = FindWorkspaceRoot();
        var fast = ctx.Args.Has("--fast");
        var runs = new List<object>();
        var allPassed = true;

        foreach (var (name, command) in AgentOperationsManifest.ResolveVerificationCommands(fast))
        {
            var result = RunShellCommand(root, command);
            runs.Add(new
            {
                name,
                command,
                passed = result.ExitCode == 0,
                exitCode = result.ExitCode,
                summary = result.ExitCode == 0
                    ? "passed"
                    : string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout.Trim() : result.Stderr.Trim(),
            });
            if (result.ExitCode != 0)
                allPassed = false;
        }

        var envelope = new
        {
            ok = allPassed,
            source = "agent-manifest",
            tier = fast ? "fast" : "full",
            manifestVersion = ManifestVersion,
            workspaceRoot = root,
            commands = runs,
        };

        var code = CliOutput.WriteEnvelope(ctx, envelope);
        return Task.FromResult(allPassed ? code : code == 0 ? 1 : code);
    }

    private static object BuildEndpointSummary(string root, EndpointSyncReport? sync = null)
    {
        sync ??= JoyZoningEndpointRegistrySync.CompareRegistryToApiFile(root);
        var agentSafe = JoyZoningEndpointRegistry.Endpoints.Count(e => e.AgentSafe);
        return new
        {
            total = JoyZoningEndpointRegistry.Endpoints.Count,
            agentSafe,
            operatorOnly = JoyZoningEndpointRegistry.Endpoints.Count - agentSafe,
            syncedWithApi = sync.Ok,
            apiRouteCount = sync.ApiRouteCount,
            missingFromRegistry = sync.MissingFromRegistry.Count,
            extraInRegistry = sync.ExtraInRegistry.Count,
        };
    }

    private static int ReadBlockedCount(object taskSummary)
    {
        var json = JsonSerializer.SerializeToElement(taskSummary, JoyZoningCliClient.JsonOptions);
        return json.TryGetProperty("blocked", out var blocked) && blocked.ValueKind == JsonValueKind.Number
            ? blocked.GetInt32()
            : 0;
    }

    private static bool IsCliPublishFresh(string root)
    {
        var publishPath = Path.Combine(root, "dist/jz-publish/jz");
        var sourcePath = Path.Combine(root, "src/JoyZoning.Cli/AgentOperationsCommand.cs");
        if (!File.Exists(publishPath) || !File.Exists(sourcePath))
            return false;

        return File.GetLastWriteTimeUtc(publishPath) >= File.GetLastWriteTimeUtc(sourcePath);
    }

    private static (int ExitCode, string Stdout, string Stderr) RunShellCommand(string workingDirectory, string command) =>
        JoyZoning.Jsdp.ShellRunner.Run(workingDirectory, command);

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
            Console.Out.WriteLine(BuildEndpointMarkdown(ctx.Args.Has("--agent-safe")));
            return 0;
        }

        var endpoints = FilterEndpoints(ctx.Args.Has("--agent-safe"));
        return CliOutput.WriteEnvelope(ctx, new
        {
            manifestVersion = ManifestVersion,
            agentSafeOnly = ctx.Args.Has("--agent-safe"),
            count = endpoints.Count,
            endpoints,
        });
    }

    private static IReadOnlyList<JoyZoningEndpointDescriptor> FilterEndpoints(bool agentSafeOnly) =>
        agentSafeOnly
            ? JoyZoningEndpointRegistry.Endpoints.Where(e => e.AgentSafe).ToList()
            : JoyZoningEndpointRegistry.Endpoints;

    private static string BuildEndpointMarkdown(bool agentSafeOnly)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# JoyZoning Endpoint Map");
        sb.AppendLine();
        if (agentSafeOnly)
            sb.AppendLine("_Agent-safe endpoints only._");
        sb.AppendLine();
        sb.AppendLine("| ID | Method | Path | Agent-safe | Purpose |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var e in FilterEndpoints(agentSafeOnly))
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

    private static async Task<object> TrySessionTaskSummaryAsync(string baseUrl, Guid sessionId)
    {
        using var client = new JoyZoningCliClient(baseUrl, TimeSpan.FromSeconds(3));
        var result = await client.ListTasksAsync(sessionId);
        if (!result.IsSuccess || result.Body is null)
            return UnavailableTaskSummary(result.Message ?? result.Error ?? "request failed");

        var body = result.Body.Value;
        var tasks = body.ValueKind == JsonValueKind.Array
            ? body
            : body.TryGetProperty("tasks", out var tasksProp) && tasksProp.ValueKind == JsonValueKind.Array
                ? tasksProp
                : default;

        if (tasks.ValueKind != JsonValueKind.Array)
            return UnavailableTaskSummary("unexpected tasks response shape");

        var total = tasks.GetArrayLength();
        var blocked = 0;
        foreach (var task in tasks.EnumerateArray())
        {
            if (!task.TryGetProperty("status", out var status))
                continue;

            if (status.ValueKind == JsonValueKind.Number && status.GetInt32() == (int)WorkTaskStatus.Blocked)
                blocked++;
            else if (status.ValueKind == JsonValueKind.String &&
                     status.GetString()?.Equals(nameof(WorkTaskStatus.Blocked), StringComparison.OrdinalIgnoreCase) == true)
                blocked++;
        }

        return new
        {
            count = total,
            blocked,
            available = true,
        };
    }

    private static object UnavailableCount(string reason) => new
    {
        count = (int?)null,
        available = false,
        reason,
    };

    private static object UnavailableTaskSummary(string reason) => new
    {
        count = (int?)null,
        blocked = (int?)null,
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
