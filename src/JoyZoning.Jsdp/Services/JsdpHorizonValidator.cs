using System.Text.Json;
using System.Text.RegularExpressions;
using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JsdpHorizonValidator
{
    private static readonly string[] RewritePatterns =
    [
        @"entire\s+(project|architecture|codebase)",
        @"complete\s+rewrite",
        @"full\s+rebuild",
        @"rewrite\s+(the\s+)?whole",
        @"rebuild\s+everything",
        @"future\s+architecture\s+beyond",
    ];

    private readonly JsdpExternalPlanValidator _nodeValidator = new();

    public JsdpHorizonValidationResult Validate(
        JsdpHorizonProposalDocument proposal,
        JsdpHorizonContext context,
        JsdpRun run,
        bool force = false)
    {
        var result = new JsdpHorizonValidationResult();
        var requested = context.RequestedNodeCount;

        if (!string.IsNullOrWhiteSpace(context.PlanningGuidance) &&
            context.PlanningGuidance.Contains("stale", StringComparison.OrdinalIgnoreCase))
            result.Warnings.Add(context.PlanningGuidance.Trim());

        if (!string.IsNullOrWhiteSpace(context.RunId) && !string.Equals(context.RunId, run.Id, StringComparison.Ordinal))
        {
            result.Errors.Add(
                $"horizon-context runId '{context.RunId}' does not match active run '{run.Id}'. Run: jz jsdp horizon export --nodes {context.RequestedNodeCount}");
            return Finalize(result);
        }

        if (context.DagSizeAtExport > 0 && context.DagSizeAtExport != run.Nodes.Count)
            result.Warnings.Add(
                $"DAG size changed since export ({context.DagSizeAtExport} → {run.Nodes.Count}). Frontier was refreshed from live run.json.");

        if (context.ActiveFailures.Count > 0 && !force)
        {
            result.Errors.Add(
                $"Active failures on node(s): {string.Join(", ", context.ActiveFailures.Select(f => f.NodeId))}. "
                + "Repair via jsdp continue before horizon import, or pass --force.");
            return Finalize(result);
        }

        if (context.CurrentFrontier.Ready.Count > 0)
            result.Warnings.Add(
                $"Ready nodes not yet verified: {string.Join(", ", context.CurrentFrontier.Ready)}. "
                + "Prefer jz jsdp next → verify → continue before extending the horizon.");

        if (context.CurrentFrontier.Blocked.Count > 0)
            result.Warnings.Add(
                $"Blocked nodes in DAG: {string.Join(", ", context.CurrentFrontier.Blocked)}. "
                + "Resolve blocked/repair chains before planning parallel expansion.");

        if (proposal.Nodes.Count == 0)
        {
            result.Errors.Add("Horizon proposal must contain at least one node.");
            return Finalize(result);
        }

        if (proposal.Nodes.Count > requested)
            result.Errors.Add(
                $"Horizon has {proposal.Nodes.Count} nodes but at most {requested} were requested.");

        ValidateContractVersion(proposal, result);
        ValidateProposalMeta(proposal, result);
        ValidateDuplicateTitles(proposal, result);
        ValidateTitlesAgainstExistingDag(proposal, run, result);

        for (var i = 0; i < proposal.Nodes.Count; i++)
        {
            var raw = proposal.Nodes[i];
            var label = string.IsNullOrWhiteSpace(raw.Id) ? $"#{i + 1}" : raw.Id.Trim();
            if (raw.AcceptanceCriteria.Count == 0)
                result.Errors.Add($"Node {label}: acceptanceCriteria required for horizon proposals.");
        }

        var plan = new ExternalJsdpPlanDocument
        {
            ContractVersion = JsdpContract.HorizonProposalVersion,
            PlanningMode = context.PlanningMode,
            Nodes = proposal.Nodes,
        };

        var nodeValidation = _nodeValidator.Validate(plan, null, run.Nodes.Keys.ToHashSet(StringComparer.Ordinal));
        result.Errors.AddRange(nodeValidation.Errors);
        result.Warnings.AddRange(nodeValidation.Warnings);

        if (nodeValidation.NormalizedNodes is null)
        {
            result.NormalizedNodes = null;
            return Finalize(result);
        }

        var existingIds = run.Nodes.Keys.ToHashSet(StringComparer.Ordinal);
        var frontier = BuildFrontierSet(context.CurrentFrontier);

        foreach (var (id, node) in nodeValidation.NormalizedNodes)
        {
            foreach (var dep in node.Dependencies)
            {
                if (!existingIds.Contains(dep) && !nodeValidation.NormalizedNodes.ContainsKey(dep))
                    result.Errors.Add($"Node {id}: dependency '{dep}' is unknown (not in run or proposal).");
            }

            if (frontier.Count > 0 && !AnchoredToFrontier(node, frontier, existingIds, nodeValidation.NormalizedNodes))
                result.Errors.Add(
                    $"Node {id}: expands beyond current frontier — depend on verified/ready nodes or prior horizon steps.");

            if (LooksLikeFullRewrite(node.Title, node.Intent, proposal))
                result.Errors.Add($"Node {id}: appears to rewrite the whole project — horizon steps must be incremental.");
        }

        if (result.Errors.Count == 0)
        {
            try
            {
                var list = nodeValidation.NormalizedNodes.Values.ToList();
                PromptDAGBuilder.WireNextLinks(list, existingIds);
                _ = PromptDAGBuilder.TopologicalOrder(list);
            }
            catch (JsdpException ex)
            {
                result.Errors.Add(ex.Message);
            }
        }

        result.NormalizedNodes = result.Errors.Count == 0 ? nodeValidation.NormalizedNodes : null;
        return Finalize(result, nodeValidation.NormalizedNodes?.Count ?? 0);
    }

    public JsdpHorizonProposalDocument LoadProposalFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new JsdpException($"Horizon proposal not found: {fullPath}");

        var size = new FileInfo(fullPath).Length;
        if (size > JsdpContract.MaxPlanFileBytes)
            throw new JsdpException($"Horizon proposal too large ({size} bytes).");

        try
        {
            var json = File.ReadAllText(fullPath);
            var proposal = JsonSerializer.Deserialize<JsdpHorizonProposalDocument>(json, JsdpJson.Options)
                ?? throw new JsdpException($"Invalid horizon proposal: {fullPath}");

            if (proposal.Nodes is null || proposal.Nodes.Count == 0)
                throw new JsdpException("Horizon proposal must include a non-empty nodes array.");

            return proposal;
        }
        catch (JsonException ex)
        {
            throw new JsdpException($"Horizon JSON parse error: {ex.Message}");
        }
    }

    public JsdpHorizonContext LoadContext(string workspaceRoot)
    {
        var path = JsdpPaths.HorizonContext(workspaceRoot);
        if (!File.Exists(path))
            throw new JsdpException("No horizon context. Run: jz jsdp horizon export --nodes 3");

        return JsdpJson.ReadFile<JsdpHorizonContext>(path);
    }

    private static void ValidateContractVersion(JsdpHorizonProposalDocument proposal, JsdpHorizonValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(proposal.ContractVersion))
        {
            result.Warnings.Add($"contractVersion omitted — recommend \"{JsdpContract.HorizonProposalVersion}\".");
            return;
        }

        if (!string.Equals(proposal.ContractVersion.Trim(), JsdpContract.HorizonProposalVersion, StringComparison.Ordinal))
            result.Errors.Add(
                $"Unsupported contractVersion: {proposal.ContractVersion} (expected {JsdpContract.HorizonProposalVersion}).");
    }

    private static void ValidateDuplicateTitles(JsdpHorizonProposalDocument proposal, JsdpHorizonValidationResult result)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in proposal.Nodes)
        {
            var title = node.Title?.Trim() ?? "";
            if (title.Length == 0)
                continue;
            if (!seen.Add(title))
                result.Errors.Add($"Duplicate horizon node title: {title}");
        }
    }

    private static void ValidateTitlesAgainstExistingDag(
        JsdpHorizonProposalDocument proposal,
        JsdpRun run,
        JsdpHorizonValidationResult result)
    {
        var existing = run.Nodes.Values
            .Select(n => n.Title?.Trim() ?? "")
            .Where(t => t.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var node in proposal.Nodes)
        {
            var title = node.Title?.Trim() ?? "";
            if (title.Length == 0)
                continue;
            if (existing.Contains(title))
                result.Errors.Add($"Horizon title duplicates existing DAG node: {title}");
        }
    }

    private static void ValidateProposalMeta(JsdpHorizonProposalDocument proposal, JsdpHorizonValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(proposal.Rationale))
            result.Warnings.Add("rationale empty — explain why these nodes are the next horizon.");

        if (string.IsNullOrWhiteSpace(proposal.StopAfter))
            result.Warnings.Add("stopAfter empty — state what should not be planned yet.");

        var combined = $"{proposal.Rationale} {proposal.StopAfter} {string.Join(' ', proposal.Assumptions)}";
        foreach (var pattern in RewritePatterns)
        {
            if (Regex.IsMatch(combined, pattern, RegexOptions.IgnoreCase))
            {
                result.Errors.Add("Proposal meta describes a full-project rewrite — rolling horizon only.");
                break;
            }
        }
    }

    private static HashSet<string> BuildFrontierSet(JsdpHorizonFrontier frontier) =>
        frontier.Verified
            .Concat(frontier.Ready)
            .ToHashSet(StringComparer.Ordinal);

    private static bool AnchoredToFrontier(
        JsdpNode node,
        HashSet<string> frontier,
        HashSet<string> existingIds,
        Dictionary<string, JsdpNode> proposed)
    {
        if (node.Dependencies.Count == 0)
            return frontier.Count == 0;

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        return DepsReachFrontier(node.Id, node.Dependencies, frontier, existingIds, proposed, visiting);
    }

    private static bool DepsReachFrontier(
        string nodeId,
        List<string> deps,
        HashSet<string> frontier,
        HashSet<string> existingIds,
        Dictionary<string, JsdpNode> proposed,
        HashSet<string> visiting)
    {
        foreach (var dep in deps)
        {
            if (frontier.Contains(dep))
                continue;

            if (existingIds.Contains(dep) && !proposed.ContainsKey(dep))
                continue;

            if (!proposed.TryGetValue(dep, out var proposedNode))
                return false;

            if (!visiting.Add(dep))
                return false;

            if (!DepsReachFrontier(dep, proposedNode.Dependencies, frontier, existingIds, proposed, visiting))
                return false;
        }

        return true;
    }

    private static bool LooksLikeFullRewrite(string title, string intent, JsdpHorizonProposalDocument proposal)
    {
        var text = $"{title} {intent} {proposal.Rationale}";
        return RewritePatterns.Any(p => Regex.IsMatch(text, p, RegexOptions.IgnoreCase));
    }

    private static JsdpHorizonValidationResult Finalize(JsdpHorizonValidationResult result, int count = 0)
    {
        result.NodeCount = count;
        result.Valid = result.Errors.Count == 0;
        return result;
    }
}
