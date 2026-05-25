using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

/// <summary>
/// External-agent planning contract: export context, validate/import plans, emit planning prompt.
/// JoyZoning owns checkpoints — no internal LLM planner.
/// </summary>
public sealed class JsdpExternalPlanningAdapter
{
    private readonly string _workspaceRoot;
    private readonly JSDPStateStore _store;
    private readonly JSDPLedger? _ledger;
    private readonly ProjectSpecAnalyzer _analyzer = new();
    private readonly RepoScanEnricher _repoScan = new();
    private readonly JsdpExternalPlanValidator _validator = new();

    public JsdpExternalPlanningAdapter(string workspaceRoot, JSDPStateStore store, JSDPLedger? ledger = null)
    {
        _workspaceRoot = workspaceRoot;
        _store = store;
        _ledger = ledger;
    }

    public JsdpExportPlanningContextResult ExportPlanningContext(JsdpPlanningMode mode)
    {
        _store.EnsureLayout();
        if (!_store.IsInitialized)
            throw new JsdpException("No JSDP run. Run: jz jsdp init");

        var spec = EnsureSpecAnalyzed();
        var config = _store.LoadConfig();
        var run = _store.LoadRun();

        var context = new JsdpPlanningContext
        {
            ContractVersion = JsdpContract.PlanningContextVersion,
            ExportedAt = DateTimeOffset.UtcNow.ToString("O"),
            WorkspaceRoot = _workspaceRoot,
            RunId = run.Id,
            Goal = run.Goal,
            PlanningMode = mode,
            SpecAnalysis = spec.Analysis,
            RepoScan = spec.RepoScan,
            Config = config,
            Constraints = spec.Analysis?.Constraints ?? [],
            NonGoals = spec.Analysis?.NonGoals ?? [],
            AcceptanceCriteria = spec.Analysis?.AcceptanceCriteria ?? [],
            Paths = new JsdpPlanningContextPaths
            {
                ProjectSpec = JsdpPaths.ProjectSpec(_workspaceRoot),
                Config = JsdpPaths.Config(_workspaceRoot),
                PlanningContext = JsdpPaths.PlanningContext(_workspaceRoot),
                Run = JsdpPaths.Run(_workspaceRoot),
                PlanSchema = JsdpPaths.PlanSchema(_workspaceRoot),
                PlanningPrompt = JsdpPaths.PlanningPromptFile(_workspaceRoot),
            },
        };

        if (run.Nodes.Count > 0)
        {
            context.ExistingDag = new JsdpPlanningContextDag
            {
                NodeCount = run.Nodes.Count,
                Convergence = PromptDAGBuilder.ComputeConvergence(run),
                Nodes = PromptDAGBuilder.BuildTreeDocument(run).Nodes,
            };
        }

        var path = JsdpPaths.PlanningContext(_workspaceRoot);
        JsdpJson.WriteFile(path, context);

        return new JsdpExportPlanningContextResult { Path = path, Context = context };
    }

    public JsdpPlanValidationResult ValidatePlan(string planPath)
    {
        var plan = _validator.LoadPlanFile(planPath);
        return _validator.Validate(plan, LoadSpecAnalysis());
    }

    public JsdpImportPlanResult ImportPlan(
        string planPath,
        JsdpPlanningMode? modeOverride = null,
        bool dryRun = false,
        bool force = false)
    {
        var fullPath = Path.GetFullPath(planPath);
        var plan = _validator.LoadPlanFile(fullPath);
        var validation = _validator.Validate(plan, LoadSpecAnalysis());

        var result = new JsdpImportPlanResult
        {
            PlanPath = fullPath,
            Validation = validation,
            DryRun = dryRun,
        };

        if (!validation.Valid || validation.NormalizedNodes is null)
            return result;

        if (!_store.IsInitialized)
            throw new JsdpException("No JSDP run. Run: jz jsdp init before import-plan.");

        var run = _store.LoadRun();
        result.ReplacedNodeCount = run.Nodes.Count;

        var verifiedCount = run.Nodes.Values.Count(n => n.Status == JsdpNodeStatus.Verified);
        if (verifiedCount > 0 && !force && !dryRun)
        {
            validation.Errors.Add(
                $"DAG has {verifiedCount} verified node(s). Re-import discards progress. Use --force to override.");
            validation.Valid = false;
            return result;
        }

        if (run.Nodes.Count > 0)
            validation.Warnings.Add(
                $"Replacing existing DAG ({run.Nodes.Count} nodes). Verified progress on removed nodes is not preserved in run.json.");

        result.NodeCount = validation.NormalizedNodes.Count;

        if (dryRun)
            return result;

        run.Nodes = validation.NormalizedNodes;
        run.PlanningMode = modeOverride ?? plan.PlanningMode ?? run.PlanningMode;
        run.CurrentNodeId = null;

        _store.SaveRun(run);
        result.Imported = true;

        _ledger?.Append(new LedgerEntry
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            NodeId = "plan-import",
            Summary = $"Imported external plan ({result.NodeCount} nodes) from {Path.GetFileName(fullPath)}",
            Verification = new LedgerVerification { Passed = true },
        });

        return result;
    }

    public JsdpPlanDiffResult DiffPlan(string planPath) => new JsdpPlanDiffService().Diff(planPath, _store);

    public JsdpPlanningPromptResult WritePlanningPrompt(JsdpPlanningMode mode)
    {
        _store.EnsureLayout();
        var export = ExportPlanningContext(mode);
        var schemaPath = WritePlanSchemaFile();
        var promptPath = JsdpPaths.PlanningPromptFile(_workspaceRoot);
        var content = JsdpPlanningPromptGenerator.Generate(export.Context, export.Path, schemaPath);
        Directory.CreateDirectory(JsdpPaths.Prompts(_workspaceRoot));
        File.WriteAllText(promptPath, content);

        return new JsdpPlanningPromptResult
        {
            PromptPath = promptPath,
            PlanningContextPath = export.Path,
            SchemaPath = schemaPath,
        };
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

    private ProjectSpecAnalysis? LoadSpecAnalysis()
    {
        if (!_store.IsInitialized)
            return null;

        try
        {
            return _store.LoadProjectSpec().Analysis;
        }
        catch
        {
            return null;
        }
    }

    private string WritePlanSchemaFile()
    {
        var path = JsdpPaths.PlanSchema(_workspaceRoot);
        Directory.CreateDirectory(JsdpPaths.State(_workspaceRoot));
        File.WriteAllText(path, JsdpPlanningPromptGenerator.SchemaDocument);
        return path;
    }
}

public static class JsdpPlanningPromptGenerator
{
    public const string SchemaDocument = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "ExternalJsdpPlanDocument",
          "type": "object",
          "required": ["nodes"],
          "properties": {
            "contractVersion": { "type": "string", "const": "1" },
            "planningMode": {
              "type": "string",
              "enum": ["vertical-slices", "systems-first", "risk-first"]
            },
            "nodes": {
              "type": "array",
              "minItems": 1,
              "maxItems": 64,
              "items": {
                "type": "object",
                "required": [
                  "title",
                  "intent",
                  "verificationCommands",
                  "allowedMutationSurface"
                ],
                "properties": {
                  "id": { "type": "string", "description": "Optional; normalized to 001, 002, …" },
                  "title": { "type": "string", "minLength": 12 },
                  "intent": { "type": "string", "minLength": 24 },
                  "prompt": { "type": "string" },
                  "dependencies": { "type": "array", "items": { "type": "string" } },
                  "acceptanceCriteria": { "type": "array", "items": { "type": "string" } },
                  "verificationCommands": {
                    "type": "array",
                    "minItems": 1,
                    "items": { "type": "string", "minLength": 1 }
                  },
                  "allowedMutationSurface": {
                    "type": "array",
                    "minItems": 1,
                    "items": { "type": "string", "minLength": 1 }
                  },
                  "outputs": { "type": "array", "items": { "type": "string" } },
                  "repairOf": { "type": "string" }
                }
              }
            }
          }
        }
        """;

    public static string Generate(JsdpPlanningContext context, string contextPath, string schemaPath)
    {
        var mode = context.PlanningMode switch
        {
            JsdpPlanningMode.SystemsFirst => "systems-first",
            JsdpPlanningMode.RiskFirst => "risk-first",
            _ => "vertical-slices",
        };

        return $"""
            # JSDP External Planning Task

            You are an **external planning agent**. JoyZoning owns validation, persistence, verification, repair, and continuation.

            **Your job:** produce `plan.json` — a project-specific JSDP DAG.  
            **Not your job:** execute nodes, skip verification, or mutate files outside the plan.

            ## Project context

            - **Goal:** {context.Goal}
            - **Planning mode:** {mode}
            - **Contract version:** {JsdpContract.ExternalPlanVersion}
            - **Workspace:** {context.WorkspaceRoot}
            - **Planning context file:** `{contextPath}`
            - **JSON schema:** `{schemaPath}`

            Read the planning context file before authoring the plan. It contains spec analysis, repo scan, config presets, constraints, and any existing DAG.

            ## Required output

            Write a single file: `plan.json` matching the schema. Set `"contractVersion": "{JsdpContract.ExternalPlanVersion}"`.

            Rules:

            1. Nodes must reference **actual** systems, files, runtime, and verification from the project — never generic placeholders like "Implement feature."
            2. Every node must include non-empty `verificationCommands` and `allowedMutationSurface` (never `.jsdp/`, `.git/`, or `node_modules/`).
            3. Dependencies must form an acyclic DAG (normalized ids: `001`, `002`, …; repair nodes: `007R1`). No self-dependencies.
            4. Small steps — resumable, verifiable, convergence-oriented (≤64 nodes).
            5. Do not include nodes that execute work; planning only.

            ## Planning mode guidance ({mode})

            {(mode == "vertical-slices" ? "Prefer end-to-end usable vertical slices." : mode == "systems-first" ? "Prefer architecture-first layering (domain → events → persistence → runtime)." : "Prefer risk spikes and feasibility proofs before feature breadth.")}

            ## Import command (operator runs after you write plan.json)

            ```bash
            jz jsdp validate-plan ./plan.json
            jz jsdp import-plan ./plan.json --dry-run   # optional preview
            jz jsdp import-plan ./plan.json
            ```

            ## Required response format

            - Summary of the DAG (node count, critical path)
            - Path to `plan.json` written
            - Verification commands reused from config/spec where applicable
            - Risks or assumptions
            """;
    }
}
