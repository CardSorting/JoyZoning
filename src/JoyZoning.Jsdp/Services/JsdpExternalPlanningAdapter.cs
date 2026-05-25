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
    private readonly JsdpExternalPlanValidator _validator = new();

    public JsdpExternalPlanningAdapter(string workspaceRoot, JSDPStateStore store)
    {
        _workspaceRoot = workspaceRoot;
        _store = store;
    }

    public JsdpExportPlanningContextResult ExportPlanningContext(JsdpPlanningMode mode)
    {
        _store.EnsureLayout();
        if (!_store.IsInitialized)
            throw new JsdpException("No JSDP run. Run: jz jsdp init");

        var spec = _store.LoadProjectSpec();
        var config = _store.LoadConfig();
        var run = _store.LoadRun();

        var context = new JsdpPlanningContext
        {
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
        ProjectSpecAnalysis? analysis = null;
        if (_store.IsInitialized)
        {
            try
            {
                analysis = _store.LoadProjectSpec().Analysis;
            }
            catch
            {
                // optional context
            }
        }

        return _validator.Validate(plan, analysis);
    }

    public JsdpImportPlanResult ImportPlan(string planPath, JsdpPlanningMode? modeOverride = null)
    {
        var validation = ValidatePlan(planPath);
        var result = new JsdpImportPlanResult
        {
            PlanPath = Path.GetFullPath(planPath),
            Validation = validation,
        };

        if (!validation.Valid || validation.NormalizedNodes is null)
            return result;

        if (!_store.IsInitialized)
            throw new JsdpException("No JSDP run. Run: jz jsdp init before import-plan.");

        var plan = _validator.LoadPlanFile(planPath);
        var run = _store.LoadRun();
        run.Nodes = validation.NormalizedNodes;
        run.PlanningMode = modeOverride ?? plan.PlanningMode ?? run.PlanningMode;
        run.CurrentNodeId = null;

        _store.SaveRun(run);
        result.Imported = true;
        result.NodeCount = run.Nodes.Count;
        return result;
    }

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

    private string WritePlanSchemaFile()
    {
        var path = JsdpPaths.PlanSchema(_workspaceRoot);
        Directory.CreateDirectory(JsdpPaths.State(_workspaceRoot));
        if (!File.Exists(path))
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
            "planningMode": {
              "type": "string",
              "enum": ["vertical-slices", "systems-first", "risk-first"]
            },
            "nodes": {
              "type": "array",
              "minItems": 1,
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
            - **Workspace:** {context.WorkspaceRoot}
            - **Planning context file:** `{contextPath}`
            - **JSON schema:** `{schemaPath}`

            Read the planning context file before authoring the plan. It contains spec analysis, repo scan, config presets, constraints, and any existing DAG.

            ## Required output

            Write a single file: `plan.json` matching the schema.

            Rules:

            1. Nodes must reference **actual** systems, files, runtime, and verification from the project — never generic placeholders like "Implement feature."
            2. Every node must include non-empty `verificationCommands` and `allowedMutationSurface`.
            3. Dependencies must form an acyclic DAG (normalized ids: `001`, `002`, …; repair nodes: `007R1`).
            4. Small steps — resumable, verifiable, convergence-oriented.
            5. Do not include nodes that execute work; planning only.

            ## Planning mode guidance ({mode})

            {(mode == "vertical-slices" ? "Prefer end-to-end usable vertical slices." : mode == "systems-first" ? "Prefer architecture-first layering (domain → events → persistence → runtime)." : "Prefer risk spikes and feasibility proofs before feature breadth.")}

            ## Import command (operator runs after you write plan.json)

            ```bash
            jz jsdp validate-plan ./plan.json
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
