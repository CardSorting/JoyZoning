using System.Text.RegularExpressions;
using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JsdpExternalPlanValidator
{
    private static readonly string[] VagueTitlePatterns =
    [
        @"^implement\s+feature\.?$",
        @"^add\s+feature\.?$",
        @"^build\s+feature\.?$",
        @"^create\s+feature\.?$",
        @"^do\s+work\.?$",
        @"^misc(ellaneous)?\.?$",
        @"^todo\b",
        @"^tbd\b",
        @"^placeholder",
        @"^fix\s+bugs\.?$",
        @"^improve\s+code\.?$",
        @"^refactor\.?$",
    ];

    public JsdpPlanValidationResult Validate(ExternalJsdpPlanDocument plan, ProjectSpecAnalysis? analysis = null)
    {
        var result = new JsdpPlanValidationResult();

        if (plan.Nodes.Count == 0)
        {
            result.Errors.Add("Plan must contain at least one node.");
            return Finalize(result);
        }

        if (plan.Nodes.Count > 64)
            result.Warnings.Add($"Large DAG ({plan.Nodes.Count} nodes). Consider smaller steps.");

        var normalized = new Dictionary<string, JsdpNode>(StringComparer.Ordinal);
        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var entries = new List<(ExternalJsdpPlanNode Raw, string Id)>();

        for (var i = 0; i < plan.Nodes.Count; i++)
        {
            var raw = plan.Nodes[i];
            var nodeIndex = i + 1;
            var normalizedId = NormalizeNodeId(raw.Id, nodeIndex);
            if (normalized.ContainsKey(normalizedId))
            {
                result.Errors.Add($"Duplicate node id after normalization: {normalizedId}");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(raw.Id))
                idMap[raw.Id.Trim()] = normalizedId;
            idMap[normalizedId] = normalizedId;

            ValidateNode(raw, normalizedId, analysis, result);

            var node = ToJsdpNode(raw, normalizedId);
            normalized[normalizedId] = node;
            entries.Add((raw, normalizedId));
        }

        foreach (var (raw, id) in entries)
        {
            var remapped = new List<string>();
            foreach (var dep in raw.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dep))
                    continue;

                var key = ResolveDependencyId(dep, idMap, normalized, out var found);
                if (!found)
                {
                    result.Errors.Add($"Node {id}: dependency '{dep}' not found.");
                    continue;
                }

                remapped.Add(key);
            }

            normalized[id].Dependencies = remapped;
        }

        if (result.Errors.Count == 0)
        {
            try
            {
                PromptDAGBuilder.WireNextLinks(normalized.Values.ToList());
                _ = PromptDAGBuilder.TopologicalOrder(normalized.Values);
            }
            catch (JsdpException ex)
            {
                result.Errors.Add(ex.Message);
            }
        }

        result.NormalizedNodes = result.Errors.Count == 0 ? normalized : null;
        return Finalize(result, normalized.Count);
    }

    public ExternalJsdpPlanDocument LoadPlanFile(string planPath)
    {
        if (!File.Exists(planPath))
            throw new JsdpException($"Plan file not found: {planPath}");

        var json = File.ReadAllText(planPath);
        var plan = System.Text.Json.JsonSerializer.Deserialize<ExternalJsdpPlanDocument>(json, JsdpJson.Options)
            ?? throw new JsdpException($"Failed to parse plan JSON: {planPath}");

        if (plan.Nodes is null || plan.Nodes.Count == 0)
            throw new JsdpException("Plan JSON must include a non-empty nodes array.");

        return plan;
    }

    private static void ValidateNode(
        ExternalJsdpPlanNode raw,
        string id,
        ProjectSpecAnalysis? analysis,
        JsdpPlanValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(raw.Title))
            result.Errors.Add($"Node {id}: title is required.");
        else if (raw.Title.Trim().Length < 12)
            result.Errors.Add($"Node {id}: title too short — must be project-specific (min 12 chars).");

        if (string.IsNullOrWhiteSpace(raw.Intent))
            result.Errors.Add($"Node {id}: intent is required.");
        else if (raw.Intent.Trim().Length < 24)
            result.Errors.Add($"Node {id}: intent too short — describe concrete scope (min 24 chars).");

        if (IsVague(raw.Title, raw.Intent))
            result.Errors.Add($"Node {id}: title/intent looks generic. Reference actual systems, files, or runtime.");

        if (raw.VerificationCommands.Count == 0)
            result.Errors.Add($"Node {id}: verificationCommands required (no silent skips).");
        else if (raw.VerificationCommands.All(string.IsNullOrWhiteSpace))
            result.Errors.Add($"Node {id}: verificationCommands cannot be empty strings.");

        if (raw.AllowedMutationSurface.Count == 0)
            result.Errors.Add($"Node {id}: allowedMutationSurface required.");
        else if (raw.AllowedMutationSurface.All(s => string.IsNullOrWhiteSpace(s)))
            result.Errors.Add($"Node {id}: allowedMutationSurface cannot be empty strings.");

        if (raw.AcceptanceCriteria.Count == 0)
            result.Warnings.Add($"Node {id}: acceptanceCriteria empty — recommended for reviewability.");

        if (analysis is not null && !ReferencesProject(raw.Title, raw.Intent, analysis))
            result.Warnings.Add($"Node {id}: weak link to project spec — prefer names from coreSystems or techStack.");
    }

    private static string ResolveDependencyId(
        string dep,
        Dictionary<string, string> idMap,
        Dictionary<string, JsdpNode> normalized,
        out bool found)
    {
        var trimmed = dep.Trim();
        if (idMap.TryGetValue(trimmed, out var mapped))
        {
            found = normalized.ContainsKey(mapped);
            return mapped;
        }

        var normalizedDep = NormalizeNodeId(trimmed, 0);
        found = normalized.ContainsKey(normalizedDep);
        return normalizedDep;
    }

    private static bool IsVague(string title, string intent)
    {
        var combined = $"{title} {intent}".ToLowerInvariant();
        foreach (var pattern in VagueTitlePatterns)
        {
            if (Regex.IsMatch(title.Trim(), pattern, RegexOptions.IgnoreCase))
                return true;
        }

        if (title.Trim().Equals(intent.Trim(), StringComparison.OrdinalIgnoreCase) && title.Length < 40)
            return true;

        return combined.Contains("implement feature", StringComparison.Ordinal)
            || combined.Contains("add feature", StringComparison.Ordinal)
            || combined.Contains("lorem ipsum", StringComparison.Ordinal);
    }

    private static bool ReferencesProject(string title, string intent, ProjectSpecAnalysis analysis)
    {
        var text = $"{title} {intent}";
        if (analysis.CoreSystems.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (analysis.TechStack.Any(s => text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (analysis.DomainConcepts.Any(s => s.Length > 3 && text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (!string.IsNullOrWhiteSpace(analysis.ProductGoal) &&
            text.Contains(analysis.ProductGoal[..Math.Min(12, analysis.ProductGoal.Length)], StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string NormalizeNodeId(string? rawId, int sequence)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return sequence.ToString("D3");

        var trimmed = rawId.Trim();
        if (Regex.IsMatch(trimmed, @"^\d{1,3}R\d*$", RegexOptions.IgnoreCase))
            return trimmed.ToUpperInvariant().Replace("r", "R", StringComparison.Ordinal);

        if (Regex.IsMatch(trimmed, @"^\d{1,3}$"))
            return int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture).ToString("D3");

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length > 0)
            return int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture).ToString("D3");

        return sequence.ToString("D3");
    }

    private static JsdpNode ToJsdpNode(ExternalJsdpPlanNode raw, string id) => new()
    {
        Id = id,
        Title = raw.Title.Trim(),
        Intent = raw.Intent.Trim(),
        Prompt = string.IsNullOrWhiteSpace(raw.Prompt) ? raw.Intent.Trim() : raw.Prompt.Trim(),
        Dependencies = raw.Dependencies.ToList(),
        AcceptanceCriteria = raw.AcceptanceCriteria.Count > 0
            ? raw.AcceptanceCriteria
            : [$"{raw.Title.Trim()} meets acceptance within allowed mutation surface."],
        VerificationCommands = raw.VerificationCommands,
        AllowedMutationSurface = raw.AllowedMutationSurface,
        Status = JsdpNodeStatus.Pending,
        Outputs = raw.Outputs?.ToList() ?? [],
        RepairOf = raw.RepairOf,
    };

    private static JsdpPlanValidationResult Finalize(JsdpPlanValidationResult result, int count = 0)
    {
        result.NodeCount = count;
        result.Valid = result.Errors.Count == 0;
        return result;
    }
}
