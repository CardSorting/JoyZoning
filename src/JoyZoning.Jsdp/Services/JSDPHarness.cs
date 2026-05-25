using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPHarness
{
    private readonly string _workspaceRoot;
    private readonly JSDPStateStore _store;
    private readonly JSDPLedger _ledger;
    private readonly ProjectSpecAnalyzer _analyzer = new();
    private readonly RepoScanEnricher _repoScan = new();
    private readonly JSDPPlanGenerator _planner = new();
    private readonly JsdpInspectService _inspect = new();
    private readonly JsdpDoctorService _doctor = new();
    private readonly JSDPNextPromptGenerator _promptGen = new();
    private readonly JSDPVerifier _verifier;
    private readonly JSDPContinuationEngine _continuation = new();
    private JsdpExternalPlanningAdapter? _planningAdapter;
    private JsdpHorizonService? _horizon;

    public JSDPHarness(string? workspaceRoot = null)
    {
        _workspaceRoot = workspaceRoot ?? WorkspaceLocator.Find();
        _store = new JSDPStateStore(_workspaceRoot);
        _ledger = new JSDPLedger(_workspaceRoot);
        _verifier = new JSDPVerifier(_workspaceRoot);
    }

    public string WorkspaceRoot => _workspaceRoot;

    public static JSDPHarness ForInit(string? workspaceRoot = null) =>
        new(WorkspaceLocator.ForInit(workspaceRoot));

    public JsdpInitResult Init(string goal, string? specPath = null)
    {
        if (_store.IsInitialized)
            throw new JsdpException("JSDP run already exists. Remove .jsdp/ to re-init.");

        _store.EnsureLayout();

        string? rawMarkdown = null;
        if (!string.IsNullOrWhiteSpace(specPath))
        {
            var fullSpec = Path.GetFullPath(specPath);
            if (!File.Exists(fullSpec))
                throw new JsdpException($"Spec file not found: {fullSpec}");
            rawMarkdown = File.ReadAllText(fullSpec);
            if (string.IsNullOrWhiteSpace(goal))
                goal = ExtractTitleFromMarkdown(rawMarkdown) ?? Path.GetFileNameWithoutExtension(fullSpec);
        }

        if (string.IsNullOrWhiteSpace(goal))
            throw new JsdpException("Goal is required: joyzoning jsdp init \"<goal>\" or --spec <file>");

        var run = new JsdpRun
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Goal = goal.Trim(),
            CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
            Nodes = new Dictionary<string, JsdpNode>(StringComparer.Ordinal),
        };

        _store.SaveRun(run);
        _store.SaveProjectSpec(new ProjectSpecDocument
        {
            Goal = goal.Trim(),
            SpecPath = specPath,
            RawMarkdown = rawMarkdown,
        });

        File.WriteAllText(JsdpPaths.Ledger(_workspaceRoot), "");

        var config = _repoScan.BuildDefaultConfig(_workspaceRoot, _repoScan.Scan(_workspaceRoot, new JsdpRepoScanOptions()));
        _store.SaveConfig(config);

        return new JsdpInitResult
        {
            RunId = run.Id,
            Goal = run.Goal,
            WorkspaceRoot = _workspaceRoot,
            SpecLoaded = rawMarkdown is not null,
        };
    }

    public ProjectSpecAnalysis Analyze()
    {
        var spec = _store.LoadProjectSpec();
        var config = _store.LoadConfig();
        var scan = _repoScan.Scan(_workspaceRoot, config.RepoScan);
        var analysis = _repoScan.Enrich(_analyzer.Analyze(spec), scan);
        spec.Analysis = analysis;
        spec.RepoScan = scan;
        _store.SaveProjectSpec(spec);

        if (analysis.VerificationStrategy.Count > 0)
        {
            config.VerificationPresets["fast"] = analysis.VerificationStrategy.Take(3).ToList();
            config.VerificationPresets["full"] = analysis.VerificationStrategy.ToList();
            _store.SaveConfig(config);
        }
        else if (config.VerificationPresets["fast"].FirstOrDefault()?.StartsWith("echo ", StringComparison.Ordinal) == true)
        {
            var refreshed = _repoScan.BuildDefaultConfig(_workspaceRoot, scan);
            refreshed.RepoScan = config.RepoScan;
            _store.SaveConfig(refreshed);
        }

        return analysis;
    }

    public JsdpRun Plan(JsdpPlanningMode mode)
    {
        var spec = _store.LoadProjectSpec();
        var config = _store.LoadConfig();
        if (spec.Analysis is null)
            spec.Analysis = Analyze();
        if (spec.RepoScan is null)
            spec.RepoScan = _repoScan.Scan(_workspaceRoot, config.RepoScan);

        var run = _store.LoadRun();
        var planned = _planner.Generate(spec.Analysis, spec.Goal, mode, config, spec.RepoScan);
        run.PlanningMode = mode;
        run.Nodes = planned.Nodes;
        run.CurrentNodeId = null;
        _store.SaveRun(run);
        return run;
    }

    public JsdpNextResult Next()
    {
        var run = _store.LoadRun();
        var nodeId = PromptDAGBuilder.FindNextReadyNode(run);
        if (nodeId is null)
            return new JsdpNextResult { Ready = false, Message = "No dependency-ready nodes." };

        var node = run.Nodes[nodeId];
        node.Status = JsdpNodeStatus.Running;
        run.CurrentNodeId = nodeId;
        var promptPath = _promptGen.WritePromptFile(_workspaceRoot, node, run);
        _store.SaveRun(run);

        return new JsdpNextResult
        {
            Ready = true,
            NodeId = nodeId,
            Title = node.Title,
            PromptPath = promptPath,
        };
    }

    public JsdpVerifyResult Verify(string? nodeId = null)
    {
        var run = _store.LoadRun();
        nodeId ??= run.CurrentNodeId;
        if (string.IsNullOrEmpty(nodeId) || !run.Nodes.TryGetValue(nodeId, out var node))
            throw new JsdpException("No node to verify. Specify --node or run jsdp next.");

        var report = _verifier.VerifyNode(node);
        var reportPath = _verifier.WriteReportMarkdown(report);

        node.Status = report.Passed ? JsdpNodeStatus.Verified : JsdpNodeStatus.Failed;
        _store.SaveRun(run);

        _ledger.Append(new LedgerEntry
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            NodeId = node.Id,
            Summary = report.Passed ? $"Verification passed for {node.Id}" : $"Verification failed for {node.Id}",
            FilesChanged = [],
            Verification = new LedgerVerification
            {
                CommandsRun = report.Results.Select(r => r.Command).ToList(),
                Passed = report.Passed,
                Failures = report.Failures,
            },
            NextRecommendation = report.Passed
                ? PromptDAGBuilder.FindNextReadyNode(run)
                : $"{node.Id}R repair recommended",
        });

        return new JsdpVerifyResult
        {
            NodeId = node.Id,
            Passed = report.Passed,
            ReportPath = reportPath,
            Failures = report.Failures,
        };
    }

    public JsdpContinuationResult Continue()
    {
        var run = _store.LoadRun();
        var result = _continuation.Continue(run);
        _store.SaveRun(run);
        return result;
    }

    public JsdpStatusReport Status()
    {
        var run = _store.LoadRun();
        return new JsdpStatusReport
        {
            Run = run,
            Convergence = PromptDAGBuilder.ComputeConvergence(run),
            NextReadyNodeId = PromptDAGBuilder.FindNextReadyNode(run),
            VerifiedNodeIds = run.Nodes.Values.Where(n => n.Status == JsdpNodeStatus.Verified).Select(n => n.Id).ToList(),
            FailedNodeIds = run.Nodes.Values.Where(n => n.Status == JsdpNodeStatus.Failed).Select(n => n.Id).ToList(),
            BlockedNodeIds = run.Nodes.Values.Where(n => n.Status == JsdpNodeStatus.Blocked).Select(n => n.Id).ToList(),
        };
    }

    public JsdpInspectReport Inspect() => _inspect.Build(_store, _ledger, _workspaceRoot);

    public JsdpDoctorReport Doctor() => _doctor.Run(_workspaceRoot, _store, _ledger);

    private JsdpExternalPlanningAdapter PlanningAdapter =>
        _planningAdapter ??= new(_workspaceRoot, _store, _ledger);

    public JsdpExportPlanningContextResult ExportPlanningContext(JsdpPlanningMode mode) =>
        PlanningAdapter.ExportPlanningContext(mode);

    public JsdpPlanValidationResult ValidatePlan(string planPath) =>
        PlanningAdapter.ValidatePlan(planPath);

    public JsdpImportPlanResult ImportPlan(string planPath, JsdpPlanningMode? mode = null, bool dryRun = false, bool force = false) =>
        PlanningAdapter.ImportPlan(planPath, mode, dryRun, force);

    public JsdpPlanDiffResult DiffPlan(string planPath) => PlanningAdapter.DiffPlan(planPath);

    public JsdpPlanningPromptResult PlanningPrompt(JsdpPlanningMode mode) =>
        PlanningAdapter.WritePlanningPrompt(mode);

    private JsdpHorizonService Horizon => _horizon ??= new(_workspaceRoot, _store, _ledger);

    public JsdpHorizonExportResult HorizonExport(int requestedNodes, JsdpPlanningMode? mode = null) =>
        Horizon.Export(requestedNodes, mode);

    public JsdpHorizonPromptResult HorizonPrompt(int requestedNodes, JsdpPlanningMode? mode = null) =>
        Horizon.WritePrompt(requestedNodes, mode);

    public JsdpHorizonValidationResult HorizonValidate(string proposalPath, int? requestedNodeOverride = null) =>
        Horizon.ValidateProposal(proposalPath, requestedNodeOverride);

    public JsdpHorizonDiffResult HorizonDiff(string proposalPath, int? requestedNodeOverride = null) =>
        Horizon.HorizonDiff(proposalPath, requestedNodeOverride);

    public JsdpHorizonImportResult HorizonImport(string proposalPath, bool dryRun = false, bool force = false) =>
        Horizon.ImportProposal(proposalPath, dryRun, force);

    public JsdpHorizonStatusReport HorizonStatus() => Horizon.Status();

    public LedgerEntry Record(string nodeId, string summary, IReadOnlyList<string>? filesChanged = null)
    {
        var run = _store.LoadRun();
        if (!run.Nodes.ContainsKey(nodeId))
            throw new JsdpException($"Unknown node: {nodeId}");

        var entry = new LedgerEntry
        {
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            NodeId = nodeId,
            Summary = summary,
            FilesChanged = filesChanged?.ToList() ?? [],
            Verification = new LedgerVerification { Passed = true, CommandsRun = [] },
        };
        _ledger.Append(entry);
        return entry;
    }

    private static string? ExtractTitleFromMarkdown(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("# ", StringComparison.Ordinal))
                return t[2..].Trim();
        }

        return null;
    }
}

public sealed class JsdpInitResult
{
    public string RunId { get; set; } = "";
    public string Goal { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public bool SpecLoaded { get; set; }
}

public sealed class JsdpNextResult
{
    public bool Ready { get; set; }
    public string? NodeId { get; set; }
    public string? Title { get; set; }
    public string? PromptPath { get; set; }
    public string Message { get; set; } = "";
}

public sealed class JsdpVerifyResult
{
    public string NodeId { get; set; } = "";
    public bool Passed { get; set; }
    public string ReportPath { get; set; } = "";
    public List<string> Failures { get; set; } = [];
}
