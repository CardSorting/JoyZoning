using System.Text;
using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

/// <summary>Rolling-horizon planning: bounded context export, validate, append-only DAG import.</summary>
public sealed class JsdpHorizonService
{
    private readonly string _workspaceRoot;
    private readonly JSDPStateStore _store;
    private readonly JSDPLedger _ledger;
    private readonly JsdpHorizonValidator _validator = new();
    private readonly ProjectSpecAnalyzer _analyzer = new();
    private readonly RepoScanEnricher _repoScan = new();

    public JsdpHorizonService(string workspaceRoot, JSDPStateStore store, JSDPLedger ledger)
    {
        _workspaceRoot = workspaceRoot;
        _store = store;
        _ledger = ledger;
    }

    public static int ClampRequestedNodes(int requested) =>
        Math.Clamp(requested, JsdpContract.MinHorizonNodes, JsdpContract.MaxHorizonNodes);

    public JsdpHorizonExportResult Export(int requestedNodes, JsdpPlanningMode? modeOverride = null)
    {
        _store.EnsureLayout();
        if (!_store.IsInitialized)
            throw new JsdpException("No JSDP run. Run: jz jsdp init");

        var requested = ClampRequestedNodes(requestedNodes);
        var run = _store.LoadRun();
        var spec = EnsureSpecAnalyzed();
        var config = _store.LoadConfig();
        var mode = modeOverride ?? run.PlanningMode;

        var frontier = ComputeFrontier(run);
        JsdpHorizonLastImport? lastImport = null;
        var lastPath = JsdpPaths.HorizonLastImport(_workspaceRoot);
        if (File.Exists(lastPath))
            lastImport = JsdpJson.ReadFile<JsdpHorizonLastImport>(lastPath);

        var failures = BuildActiveFailures(run);
        var context = new JsdpHorizonContext
        {
            ExportedAt = DateTimeOffset.UtcNow.ToString("O"),
            ProjectSummary = BuildProjectSummary(spec, run.Goal),
            PlanningMode = mode,
            CurrentFrontier = frontier,
            RecentLedgerSummaries = BuildRecentLedgerSummaries(),
            ActiveFailures = failures,
            RepoSummary = BuildRepoSummary(spec.RepoScan, config),
            RequestedNodeCount = requested,
            ExistingNodeIds = run.Nodes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            RunId = run.Id,
            DagSizeAtExport = run.Nodes.Count,
            PreviousStopAfter = lastImport?.StopAfter,
            PlanningGuidance = BuildPlanningGuidance(frontier, failures),
        };

        var projectDoc = new JsdpProjectSummaryDocument
        {
            Summary = context.ProjectSummary,
            Goal = run.Goal,
            ExportedAt = context.ExportedAt,
        };
        var repoDoc = new JsdpRepoSummaryDocument
        {
            RepoSummary = context.RepoSummary,
            ExportedAt = context.ExportedAt,
        };
        var frontierDoc = new JsdpFrontierDocument
        {
            Frontier = frontier,
            ExportedAt = context.ExportedAt,
        };

        JsdpJson.WriteFile(JsdpPaths.ProjectSummary(_workspaceRoot), projectDoc);
        JsdpJson.WriteFile(JsdpPaths.RepoSummary(_workspaceRoot), repoDoc);
        JsdpJson.WriteFile(JsdpPaths.Frontier(_workspaceRoot), frontierDoc);
        var ctxPath = JsdpPaths.HorizonContext(_workspaceRoot);
        JsdpJson.WriteFile(ctxPath, context);
        var ctxBytes = new FileInfo(ctxPath).Length;

        var warnings = new List<string>();
        if (ctxBytes > JsdpContract.MaxHorizonContextBytes)
            warnings.Add(
                $"horizon-context.json is {ctxBytes} bytes (budget {JsdpContract.MaxHorizonContextBytes}). "
                + "Trim project spec or reduce ledger noise before automation.");

        return new JsdpHorizonExportResult
        {
            HorizonContextPath = ctxPath,
            ProjectSummaryPath = JsdpPaths.ProjectSummary(_workspaceRoot),
            RepoSummaryPath = JsdpPaths.RepoSummary(_workspaceRoot),
            FrontierPath = JsdpPaths.Frontier(_workspaceRoot),
            ContextByteSize = (int)ctxBytes,
            ContextWithinBudget = ctxBytes <= JsdpContract.MaxHorizonContextBytes,
            Warnings = warnings,
            Context = context,
        };
    }

    public JsdpHorizonPromptResult WritePrompt(int requestedNodes, JsdpPlanningMode? modeOverride = null)
    {
        var export = Export(requestedNodes, modeOverride);
        var schemaPath = WriteHorizonSchema();
        var promptPath = JsdpPaths.HorizonPromptFile(_workspaceRoot);
        var content = JsdpHorizonPromptGenerator.Generate(export.Context, export.HorizonContextPath, schemaPath);
        Directory.CreateDirectory(JsdpPaths.Prompts(_workspaceRoot));
        File.WriteAllText(promptPath, content);

        return new JsdpHorizonPromptResult
        {
            PromptPath = promptPath,
            HorizonContextPath = export.HorizonContextPath,
            SchemaPath = schemaPath,
        };
    }

    public JsdpHorizonValidationResult ValidateProposal(string proposalPath, bool refreshContext = true)
    {
        var proposal = _validator.LoadProposalFile(proposalPath);
        var run = _store.LoadRun();
        var context = PrepareContextForValidation(refreshContext, run);
        return _validator.Validate(proposal, context, run);
    }

    public JsdpHorizonImportResult ImportProposal(string proposalPath, bool dryRun = false, bool force = false)
    {
        var fullPath = Path.GetFullPath(proposalPath);
        var proposal = _validator.LoadProposalFile(fullPath);
        var run = _store.LoadRun();
        var context = PrepareContextForValidation(true, run);
        var validation = _validator.Validate(proposal, context, run, force);

        var result = new JsdpHorizonImportResult
        {
            ProposalPath = fullPath,
            Validation = validation,
            TotalNodeCount = run.Nodes.Count,
            DryRun = dryRun,
        };

        if (!validation.Valid || validation.NormalizedNodes is null)
            return result;

        var idMap = BuildAppendIdMap(run, validation.NormalizedNodes);
        var projectedIds = idMap.Values.OrderBy(x => x, StringComparer.Ordinal).ToList();
        result.ProjectedNodeIds = projectedIds;

        if (dryRun)
        {
            result.AppendedNodeCount = projectedIds.Count;
            result.AppendedNodeIds = projectedIds;
            return result;
        }

        var appendedIds = new List<string>();

        foreach (var (tempId, node) in validation.NormalizedNodes)
        {
            var finalId = idMap[tempId];
            node.Id = finalId;
            node.Dependencies = node.Dependencies.Select(d => idMap.TryGetValue(d, out var mapped) ? mapped : d).ToList();
            node.Status = JsdpNodeStatus.Pending;
            run.Nodes[finalId] = node;
            appendedIds.Add(finalId);
        }

        _store.SaveRun(run);
        result.Imported = true;
        result.AppendedNodeCount = appendedIds.Count;
        result.AppendedNodeIds = appendedIds;
        result.TotalNodeCount = run.Nodes.Count;

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        result.ReportPath = WriteImportReport(stamp, proposal, result, context);

        var lastImport = new JsdpHorizonLastImport
        {
            ImportedAt = DateTimeOffset.UtcNow.ToString("O"),
            ProposalPath = fullPath,
            AppendedNodes = appendedIds.Count,
            NodeIds = appendedIds,
            StopAfter = proposal.StopAfter,
            Rationale = proposal.Rationale,
        };
        JsdpJson.WriteFile(JsdpPaths.HorizonLastImport(_workspaceRoot), lastImport);
        JsdpJson.WriteFile(JsdpPaths.HorizonProposal(_workspaceRoot), proposal);

        _ledger.Append(new LedgerEntry
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            NodeId = "horizon-import",
            Summary = $"Horizon import: +{appendedIds.Count} nodes ({string.Join(", ", appendedIds)})",
            Verification = new LedgerVerification { Passed = true },
        });

        result.ContextRefreshed = RefreshHorizonSnapshotAfterImport(run, context, proposal);
        return result;
    }

    public JsdpHorizonStatusReport Status()
    {
        if (!_store.IsInitialized)
            throw new JsdpException("No JSDP run. Run: jz jsdp init");

        var run = _store.LoadRun();
        var frontier = ComputeFrontier(run);
        var ready = frontier.Ready;
        var blocked = run.Nodes.Values
            .Where(n => n.Status == JsdpNodeStatus.Blocked)
            .Select(n => n.Id)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        var failed = frontier.Failed;

        JsdpHorizonLastImport? last = null;
        var lastPath = JsdpPaths.HorizonLastImport(_workspaceRoot);
        if (File.Exists(lastPath))
            last = JsdpJson.ReadFile<JsdpHorizonLastImport>(lastPath);

        var suggested = SuggestHorizonSize(run, frontier);
        var contextStale = IsHorizonContextStale();
        var ctxBytes = File.Exists(JsdpPaths.HorizonContext(_workspaceRoot))
            ? (int)new FileInfo(JsdpPaths.HorizonContext(_workspaceRoot)).Length
            : 0;

        return new JsdpHorizonStatusReport
        {
            HorizonContextStale = contextStale,
            HasActiveFailures = failed.Count > 0,
            HasReadyNodes = ready.Count > 0,
            ContextByteSize = ctxBytes,
            SuggestedAction = SuggestNextAction(frontier, contextStale, failed.Count > 0),
            DagSize = run.Nodes.Count,
            ReadyNodeIds = ready,
            BlockedNodeIds = blocked,
            FailedNodeIds = failed,
            LastHorizonImport = last,
            SuggestedNextHorizonSize = suggested,
            Frontier = frontier,
        };
    }

    private JsdpHorizonContext PrepareContextForValidation(bool refreshFromDisk, JsdpRun run)
    {
        JsdpHorizonContext context;
        if (refreshFromDisk && File.Exists(JsdpPaths.HorizonContext(_workspaceRoot)))
        {
            context = _validator.LoadContext(_workspaceRoot);
            if (IsHorizonContextStale())
                context.PlanningGuidance = (context.PlanningGuidance ?? "") +
                    " WARNING: horizon-context.json is stale — frontier refreshed from live run.json.";
        }
        else
        {
            context = new JsdpHorizonContext
            {
                RequestedNodeCount = JsdpContract.DefaultHorizonNodes,
                ProjectSummary = BuildProjectSummary(_store.LoadProjectSpec(), run.Goal),
                PlanningMode = run.PlanningMode,
            };
        }

        context.CurrentFrontier = ComputeFrontier(run);
        context.ActiveFailures = BuildActiveFailures(run);
        context.ExistingNodeIds = run.Nodes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        return context;
    }

    private bool IsHorizonContextStale()
    {
        var ctxPath = JsdpPaths.HorizonContext(_workspaceRoot);
        var runPath = JsdpPaths.Run(_workspaceRoot);
        if (!File.Exists(ctxPath) || !File.Exists(runPath))
            return false;

        return File.GetLastWriteTimeUtc(runPath) > File.GetLastWriteTimeUtc(ctxPath).AddSeconds(2);
    }

    private static string? BuildPlanningGuidance(JsdpHorizonFrontier frontier, List<JsdpHorizonActiveFailure> failures)
    {
        if (failures.Count > 0)
            return "Repair failed nodes before extending horizon (jz jsdp continue).";

        if (frontier.Ready.Count > 0)
            return $"Execute ready nodes first: {string.Join(", ", frontier.Ready)}.";

        if (frontier.Verified.Count == 0 && frontier.Ready.Count == 0)
            return "Seed or import an initial DAG before rolling horizon.";

        return null;
    }

    private bool RefreshHorizonSnapshotAfterImport(
        JsdpRun run,
        JsdpHorizonContext context,
        JsdpHorizonProposalDocument proposal)
    {
        context.ExportedAt = DateTimeOffset.UtcNow.ToString("O");
        context.CurrentFrontier = ComputeFrontier(run);
        context.ActiveFailures = BuildActiveFailures(run);
        context.ExistingNodeIds = run.Nodes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
        context.DagSizeAtExport = run.Nodes.Count;
        context.RunId = run.Id;
        context.PreviousStopAfter = proposal.StopAfter;
        context.PlanningGuidance = BuildPlanningGuidance(context.CurrentFrontier, context.ActiveFailures);

        JsdpJson.WriteFile(JsdpPaths.HorizonContext(_workspaceRoot), context);
        JsdpJson.WriteFile(JsdpPaths.Frontier(_workspaceRoot), new JsdpFrontierDocument
        {
            Frontier = context.CurrentFrontier,
            ExportedAt = context.ExportedAt,
        });
        return true;
    }

    private static string? SuggestNextAction(JsdpHorizonFrontier frontier, bool contextStale, bool hasFailures)
    {
        if (hasFailures)
            return "jz jsdp continue  # repair failed nodes before horizon export";
        if (frontier.Ready.Count > 0)
            return "jz jsdp next  # execute ready nodes before next horizon";
        if (contextStale)
            return "jz jsdp horizon export --nodes 3  # refresh stale context";
        if (frontier.Verified.Count == 0 && frontier.Ready.Count == 0)
            return "jz jsdp horizon export --nodes 3  # seed or extend DAG";
        return "jz jsdp horizon export --nodes 3  # plan next bounded horizon";
    }

    private static int SuggestHorizonSize(JsdpRun run, JsdpHorizonFrontier frontier)
    {
        if (run.Nodes.Count == 0)
            return JsdpContract.DefaultHorizonNodes;
        if (frontier.Failed.Count > 0)
            return JsdpContract.MinHorizonNodes;
        if (frontier.Ready.Count >= JsdpContract.MaxHorizonNodes)
            return JsdpContract.MinHorizonNodes;
        return JsdpContract.DefaultHorizonNodes;
    }

    private JsdpHorizonFrontier ComputeFrontier(JsdpRun run)
    {
        var verified = run.Nodes.Values
            .Where(n => n.Status == JsdpNodeStatus.Verified)
            .Select(n => n.Id)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var failed = run.Nodes.Values
            .Where(n => n.Status == JsdpNodeStatus.Failed)
            .Select(n => n.Id)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var blocked = run.Nodes.Values
            .Where(n => n.Status == JsdpNodeStatus.Blocked)
            .Select(n => n.Id)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var ready = new List<string>();
        foreach (var node in PromptDAGBuilder.TopologicalOrder(run.Nodes.Values))
        {
            if (node.Status != JsdpNodeStatus.Pending)
                continue;

            var depsMet = node.Dependencies.All(dep =>
                run.Nodes.TryGetValue(dep, out var depNode) &&
                depNode.Status == JsdpNodeStatus.Verified);

            if (depsMet)
                ready.Add(node.Id);
        }

        return new JsdpHorizonFrontier
        {
            Verified = verified,
            Ready = ready,
            Blocked = blocked,
            Failed = failed,
        };
    }

    private List<JsdpHorizonLedgerSummary> BuildRecentLedgerSummaries()
    {
        return _ledger.ReadAll()
            .Where(e => e.NodeId is not "plan-import" and not "horizon-import")
            .TakeLast(JsdpContract.MaxHorizonLedgerSummaries)
            .Select(e => new JsdpHorizonLedgerSummary
            {
                NodeId = e.NodeId,
                Summary = e.Summary,
                VerificationPassed = e.Verification.Passed,
            })
            .ToList();
    }

    private static List<JsdpHorizonActiveFailure> BuildActiveFailures(JsdpRun run) =>
        run.Nodes.Values
            .Where(n => n.Status == JsdpNodeStatus.Failed)
            .Select(n => new JsdpHorizonActiveFailure
            {
                NodeId = n.Id,
                FailureSummary = $"Node {n.Id} failed verification — repair via jsdp continue before extending horizon.",
            })
            .ToList();

    private static JsdpHorizonRepoSummary BuildRepoSummary(RepoScanSnapshot? scan, JsdpConfig config)
    {
        if (scan is null)
        {
            return new JsdpHorizonRepoSummary
            {
                TestCommands = config.VerificationPresets.GetValueOrDefault("fast") ?? [],
            };
        }

        var stack = new List<string>();
        if (!string.IsNullOrWhiteSpace(scan.PrimaryStack))
            stack.Add(scan.PrimaryStack);
        if (scan.SolutionFiles.Count > 0)
            stack.AddRange(scan.SolutionFiles.Take(2));

        var paths = scan.KeyFiles
            .Concat(scan.TopLevelDirectories.Take(8))
            .Concat(scan.SuggestedMutationSurfaces.Take(6))
            .Distinct(StringComparer.Ordinal)
            .Take(16)
            .ToList();

        var tests = scan.SuggestedVerification
            .Concat(config.VerificationPresets.GetValueOrDefault("fast") ?? [])
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToList();

        return new JsdpHorizonRepoSummary
        {
            DetectedStack = stack,
            ImportantPaths = paths,
            TestCommands = tests,
        };
    }

    private static string BuildProjectSummary(ProjectSpecDocument spec, string goal)
    {
        var analysis = spec.Analysis;
        if (analysis is null)
            return $"Goal: {goal}";

        var systems = analysis.CoreSystems.Count > 0
            ? string.Join(", ", analysis.CoreSystems.Take(6))
            : "n/a";
        var stack = analysis.TechStack.Count > 0
            ? string.Join(", ", analysis.TechStack.Take(6))
            : "n/a";

        return $"""
            Goal: {goal}
            Product: {analysis.ProductGoal}
            Core systems: {systems}
            Tech stack: {stack}
            Constraints: {string.Join("; ", analysis.Constraints.Take(4))}
            Non-goals: {string.Join("; ", analysis.NonGoals.Take(3))}
            """;
    }

    private ProjectSpecDocument EnsureSpecAnalyzed()
    {
        var spec = _store.LoadProjectSpec();
        if (spec.Analysis is not null)
            return spec;

        var config = _store.LoadConfig();
        var scan = _repoScan.Scan(_workspaceRoot, config.RepoScan);
        spec.Analysis = _repoScan.Enrich(_analyzer.Analyze(spec), scan);
        spec.RepoScan = scan;
        _store.SaveProjectSpec(spec);
        return spec;
    }

    private static Dictionary<string, string> BuildAppendIdMap(
        JsdpRun run,
        Dictionary<string, JsdpNode> normalized)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var next = AllocateNextNumericId(run);

        foreach (var id in normalized.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            while (run.Nodes.ContainsKey(next))
                next = IncrementId(next);

            map[id] = next;
            next = IncrementId(next);
        }

        return map;
    }

    private static string AllocateNextNumericId(JsdpRun run)
    {
        var max = 0;
        foreach (var id in run.Nodes.Keys)
        {
            var baseId = id.Split('R')[0];
            if (int.TryParse(baseId, out var n))
                max = Math.Max(max, n);
        }

        return (max + 1).ToString("D3");
    }

    private static string IncrementId(string id)
    {
        var baseId = id.Split('R')[0];
        if (int.TryParse(baseId, out var n))
            return (n + 1).ToString("D3");
        return id + "X";
    }

    private string WriteImportReport(
        string stamp,
        JsdpHorizonProposalDocument proposal,
        JsdpHorizonImportResult result,
        JsdpHorizonContext context)
    {
        var path = JsdpPaths.HorizonImportReport(_workspaceRoot, stamp);
        Directory.CreateDirectory(JsdpPaths.Reports(_workspaceRoot));

        var sb = new StringBuilder();
        sb.AppendLine($"# Horizon Import Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTimeOffset.UtcNow:O}");
        sb.AppendLine($"**Appended:** {result.AppendedNodeCount} node(s)");
        sb.AppendLine($"**Total DAG size:** {result.TotalNodeCount}");
        sb.AppendLine();
        sb.AppendLine("## Node IDs");
        foreach (var id in result.AppendedNodeIds)
            sb.AppendLine($"- `{id}`");
        sb.AppendLine();
        sb.AppendLine("## Rationale");
        sb.AppendLine(proposal.Rationale);
        sb.AppendLine();
        sb.AppendLine("## Stop after");
        sb.AppendLine(proposal.StopAfter);
        sb.AppendLine();
        sb.AppendLine("## Assumptions");
        foreach (var a in proposal.Assumptions)
            sb.AppendLine($"- {a}");
        sb.AppendLine();
        sb.AppendLine($"Horizon requested: {context.RequestedNodeCount} nodes");

        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private string WriteHorizonSchema()
    {
        var path = JsdpPaths.HorizonSchema(_workspaceRoot);
        Directory.CreateDirectory(JsdpPaths.State(_workspaceRoot));
        File.WriteAllText(path, JsdpHorizonPromptGenerator.SchemaDocument);
        return path;
    }
}

public static class JsdpHorizonPromptGenerator
{
    public const string SchemaDocument = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "JsdpHorizonProposalDocument",
          "type": "object",
          "required": ["nodes", "rationale", "stopAfter"],
          "properties": {
            "contractVersion": { "type": "string", "const": "1" },
            "nodes": {
              "type": "array",
              "minItems": 1,
              "maxItems": 5,
              "items": {
                "type": "object",
                "required": [
                  "title",
                  "intent",
                  "acceptanceCriteria",
                  "verificationCommands",
                  "allowedMutationSurface"
                ],
                "properties": {
                  "id": { "type": "string" },
                  "title": { "type": "string", "minLength": 12 },
                  "intent": { "type": "string", "minLength": 24 },
                  "dependencies": { "type": "array", "items": { "type": "string" } },
                  "acceptanceCriteria": { "type": "array", "minItems": 1, "items": { "type": "string" } },
                  "verificationCommands": { "type": "array", "minItems": 1, "items": { "type": "string" } },
                  "allowedMutationSurface": { "type": "array", "minItems": 1, "items": { "type": "string" } }
                }
              }
            },
            "rationale": { "type": "string", "minLength": 12 },
            "assumptions": { "type": "array", "items": { "type": "string" } },
            "stopAfter": { "type": "string", "minLength": 8 }
          }
        }
        """;

    public static string Generate(JsdpHorizonContext context, string contextPath, string schemaPath)
    {
        var existingIds = string.Join(", ", context.ExistingNodeIds.DefaultIfEmpty("(none)"));
        var verified = string.Join(", ", context.CurrentFrontier.Verified.DefaultIfEmpty("none"));
        var ready = string.Join(", ", context.CurrentFrontier.Ready.DefaultIfEmpty("none"));
        var failed = string.Join(", ", context.CurrentFrontier.Failed.DefaultIfEmpty("none"));
        var blocked = string.Join(", ", context.CurrentFrontier.Blocked.DefaultIfEmpty("none"));

        return $"""
            # JSDP Rolling Horizon Planning

            You are **not** planning the whole project.
            You are planning only the **next bounded JSDP horizon**.

            Return **JSON only** (`horizon.json`). No markdown wrapper.

            ## Limits

            - Generate **at most {context.RequestedNodeCount}** nodes (hard limit).
            - Each node must be small, verifiable, project-specific, and dependency-aware.
            - Do **not** include future architecture beyond this horizon.
            - Do **not** rewrite existing verified work.
            - Dependencies may reference existing node IDs: {existingIds}

            ## Context file

            Read `{contextPath}` — contains project summary, frontier, recent ledger summaries, active failures, repo summary.

            ## Schema

            `{schemaPath}` — set `"contractVersion": "{JsdpContract.HorizonProposalVersion}"`.

            ## Frontier (do not skip ahead)

            - Verified: {verified}
            - Ready: {ready}
            - Failed: {failed}
            - Blocked: {blocked}

            {(string.IsNullOrWhiteSpace(context.PlanningGuidance) ? "" : $"\n## Operator guidance\n\n{context.PlanningGuidance}\n")}

            {(string.IsNullOrWhiteSpace(context.PreviousStopAfter) ? "" : $"\n## Previous horizon stopAfter\n\n{context.PreviousStopAfter}\n")}

            ## Project summary

            ```
            {context.ProjectSummary.Trim()}
            ```

            ## Required JSON shape

            See schema file for `nodes`, `rationale`, `assumptions`, and `stopAfter`.

            ## Import (operator)

            ```bash
            jz jsdp horizon validate ./horizon.json
            jz jsdp horizon import ./horizon.json
            ```
            """;
    }
}
