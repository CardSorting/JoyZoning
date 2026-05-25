using System.Text.Json;
using System.Text.RegularExpressions;
using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JsdpExternalPlanValidator
{
    private static readonly string[] VaguePatterns =
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
        @"^update\s+code\.?$",
        @"^work\s+on\s+",
    ];

    private static readonly string[] ProtectedSurfaces = [".jsdp/", ".git/", "node_modules/"];

    private static readonly Regex[] DangerousCommandPatterns =
    [
        new(@"rm\s+(-[a-zA-Z]*f|--force|-[a-zA-Z]*r)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bdd\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\bmkfs\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@">\s*/dev/", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"curl\s+.*\|\s*(ba)?sh", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public JsdpPlanValidationResult Validate(
        ExternalJsdpPlanDocument plan,
        ProjectSpecAnalysis? analysis = null,
        IReadOnlySet<string>? externalNodeIds = null)
    {
        var result = new JsdpPlanValidationResult();

        if (plan.Nodes.Count == 0)
        {
            result.Errors.Add("Plan must contain at least one node.");
            return Finalize(result);
        }

        if (plan.Nodes.Count > JsdpContract.MaxPlanNodes)
            result.Errors.Add($"Plan exceeds maximum {JsdpContract.MaxPlanNodes} nodes.");

        ValidateContractVersion(plan, result);

        if (plan.PlanningMode is null)
            result.Warnings.Add("planningMode omitted — import will keep existing run planning mode.");
        else if (!Enum.IsDefined(plan.PlanningMode.Value))
            result.Errors.Add($"Invalid planningMode: {plan.PlanningMode}");

        var normalized = new Dictionary<string, JsdpNode>(StringComparer.Ordinal);
        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        if (externalNodeIds is not null)
        {
            foreach (var id in externalNodeIds)
                idMap[id] = id;
        }

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

        if (entries.Count != plan.Nodes.Count)
            result.Errors.Add("Plan has structural errors (duplicate ids or invalid nodes). No nodes imported.");

        foreach (var (raw, id) in entries)
        {
            if (!string.IsNullOrWhiteSpace(raw.RepairOf))
            {
                var repairKey = raw.RepairOf.Trim();
                if (!idMap.ContainsKey(repairKey))
                    result.Errors.Add($"Node {id}: repairOf '{repairKey}' does not match any node id in plan.");
            }
        }

        foreach (var (raw, id) in entries)
        {
            var remapped = new List<string>();
            foreach (var dep in raw.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(dep))
                    continue;

                var key = ResolveDependencyId(dep, idMap, normalized, out var found);
                if (!found && !(externalNodeIds?.Contains(dep.Trim()) == true))
                {
                    result.Errors.Add($"Node {id}: dependency '{dep}' not found.");
                    continue;
                }

                if (!found && externalNodeIds?.Contains(dep.Trim()) == true)
                    key = dep.Trim();

                if (string.Equals(key, id, StringComparison.Ordinal))
                {
                    result.Errors.Add($"Node {id}: cannot depend on itself.");
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
                var list = normalized.Values.ToList();
                PromptDAGBuilder.WireNextLinks(list, externalNodeIds);
                _ = PromptDAGBuilder.TopologicalOrder(list);
                WarnUnreachableNodes(normalized, result);
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
        var fullPath = Path.GetFullPath(planPath);
        if (!File.Exists(fullPath))
            throw new JsdpException($"Plan file not found: {fullPath}");

        var size = new FileInfo(fullPath).Length;
        if (size > JsdpContract.MaxPlanFileBytes)
            throw new JsdpException(
                $"Plan file too large ({size} bytes). Maximum is {JsdpContract.MaxPlanFileBytes} bytes.");

        try
        {
            var json = File.ReadAllText(fullPath);
            var plan = JsonSerializer.Deserialize<ExternalJsdpPlanDocument>(json, JsdpJson.Options)
                ?? throw new JsdpException($"Plan JSON is empty or invalid: {fullPath}");

            if (plan.Nodes is null || plan.Nodes.Count == 0)
                throw new JsdpException("Plan JSON must include a non-empty nodes array.");

            return plan;
        }
        catch (JsonException ex)
        {
            throw new JsdpException($"Plan JSON parse error: {ex.Message}");
        }
    }

    private static void ValidateNode(
        ExternalJsdpPlanNode raw,
        string id,
        ProjectSpecAnalysis? analysis,
        JsdpPlanValidationResult result)
    {
        var title = raw.Title?.Trim() ?? "";
        var intent = raw.Intent?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title))
            result.Errors.Add($"Node {id}: title is required.");
        else if (title.Length < 12)
            result.Errors.Add($"Node {id}: title too short — must be project-specific (min 12 chars).");

        if (string.IsNullOrWhiteSpace(intent))
            result.Errors.Add($"Node {id}: intent is required.");
        else if (intent.Length < 24)
            result.Errors.Add($"Node {id}: intent too short — describe concrete scope (min 24 chars).");

        if (IsVague(title, intent))
            result.Errors.Add($"Node {id}: title/intent looks generic. Reference actual systems, files, or runtime.");

        var verify = SanitizeList(raw.VerificationCommands);
        if (verify.Count == 0)
            result.Errors.Add($"Node {id}: verificationCommands required (no silent skips).");
        else if (verify.All(c => c.StartsWith("echo ", StringComparison.OrdinalIgnoreCase)))
            result.Warnings.Add($"Node {id}: verification is echo-only — prefer real project commands.");
        else
        {
            foreach (var cmd in verify.Where(LooksDangerous))
                result.Warnings.Add($"Node {id}: verification command looks destructive: {cmd}");
        }

        if (!string.IsNullOrWhiteSpace(raw.RepairOf))
            result.Warnings.Add($"Node {id}: repairOf is set — repair nodes are normally created by jsdp continue, not external plans.");

        var surfaces = SanitizeList(raw.AllowedMutationSurface);
        if (surfaces.Count == 0)
            result.Errors.Add($"Node {id}: allowedMutationSurface required.");
        else if (surfaces.Any(IsProtectedSurface))
            result.Errors.Add($"Node {id}: allowedMutationSurface must not include protected paths (.jsdp/, .git/, node_modules/).");

        if (raw.AcceptanceCriteria.Count == 0)
            result.Warnings.Add($"Node {id}: acceptanceCriteria empty — recommended for reviewability.");

        if (analysis is not null && !ReferencesProject(title, intent, analysis))
            result.Warnings.Add($"Node {id}: weak link to project spec — prefer names from coreSystems or techStack.");
    }

    private static bool IsProtectedSurface(string surface)
    {
        var s = surface.Trim().Replace('\\', '/');
        if (!s.EndsWith('/'))
            s += "/";
        return ProtectedSurfaces.Any(p => s.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> SanitizeList(IEnumerable<string> items) =>
        items
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

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

        if (Regex.IsMatch(trimmed, @"^\d{1,3}R\d*$", RegexOptions.IgnoreCase))
        {
            var repairId = NormalizeNodeId(trimmed, 0);
            found = normalized.ContainsKey(repairId);
            return repairId;
        }

        if (Regex.IsMatch(trimmed, @"^\d{1,3}$"))
        {
            var normalizedDep = NormalizeNodeId(trimmed, 0);
            found = normalized.ContainsKey(normalizedDep);
            return normalizedDep;
        }

        found = false;
        return trimmed;
    }

    private static bool IsVague(string title, string intent)
    {
        foreach (var pattern in VaguePatterns)
        {
            if (Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase))
                return true;
            if (Regex.IsMatch(intent, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        var combined = $"{title} {intent}".ToLowerInvariant();
        if (title.Equals(intent, StringComparison.OrdinalIgnoreCase) && title.Length < 40)
            return true;

        return combined.Contains("implement feature", StringComparison.Ordinal)
            || combined.Contains("add feature", StringComparison.Ordinal)
            || combined.Contains("lorem ipsum", StringComparison.Ordinal)
            || combined.Contains("as needed", StringComparison.Ordinal);
    }

    private static bool ReferencesProject(string title, string intent, ProjectSpecAnalysis analysis)
    {
        var text = $"{title} {intent}";
        if (analysis.CoreSystems.Any(s => s.Length > 2 && text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (analysis.TechStack.Any(s => s.Length > 2 && text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (analysis.DomainConcepts.Any(s => s.Length > 3 && text.Contains(s, StringComparison.OrdinalIgnoreCase)))
            return true;
        if (!string.IsNullOrWhiteSpace(analysis.ProductGoal))
        {
            var token = analysis.ProductGoal.Length >= 8
                ? analysis.ProductGoal[..8]
                : analysis.ProductGoal;
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string NormalizeNodeId(string? rawId, int sequence)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return sequence.ToString("D3");

        var trimmed = rawId.Trim();
        if (Regex.IsMatch(trimmed, @"^\d{1,3}R\d+$", RegexOptions.IgnoreCase))
            return trimmed[..trimmed.IndexOf('R', StringComparison.OrdinalIgnoreCase)].PadLeft(3, '0')
                + "R" + trimmed[(trimmed.IndexOf('R', StringComparison.OrdinalIgnoreCase) + 1)..];

        if (Regex.IsMatch(trimmed, @"^\d{1,3}$"))
            return int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture).ToString("D3");

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length > 0)
            return int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture).ToString("D3");

        return sequence.ToString("D3");
    }

    private static void ValidateContractVersion(ExternalJsdpPlanDocument plan, JsdpPlanValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(plan.ContractVersion))
        {
            result.Warnings.Add($"contractVersion omitted — recommend \"{JsdpContract.ExternalPlanVersion}\".");
            return;
        }

        if (!string.Equals(plan.ContractVersion.Trim(), JsdpContract.ExternalPlanVersion, StringComparison.Ordinal))
            result.Errors.Add(
                $"Unsupported contractVersion: {plan.ContractVersion} (expected {JsdpContract.ExternalPlanVersion}).");
    }

    private static void WarnUnreachableNodes(
        Dictionary<string, JsdpNode> normalized,
        JsdpPlanValidationResult result)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        void Mark(string id)
        {
            if (!reachable.Add(id))
                return;
            foreach (var dependent in normalized.Values.Where(n => n.Dependencies.Contains(id, StringComparer.Ordinal)))
                Mark(dependent.Id);
        }

        foreach (var root in normalized.Values.Where(n => n.Dependencies.Count == 0))
            Mark(root.Id);

        foreach (var id in normalized.Keys)
        {
            if (!reachable.Contains(id))
                result.Warnings.Add($"Node {id} is unreachable from root nodes — check dependencies.");
        }
    }

    private static bool LooksDangerous(string command) =>
        DangerousCommandPatterns.Any(p => p.IsMatch(command));

    private static JsdpNode ToJsdpNode(ExternalJsdpPlanNode raw, string id) => new()
    {
        Id = id,
        Title = (raw.Title ?? "").Trim(),
        Intent = (raw.Intent ?? "").Trim(),
        Prompt = string.IsNullOrWhiteSpace(raw.Prompt) ? (raw.Intent ?? "").Trim() : raw.Prompt.Trim(),
        Dependencies = [],
        AcceptanceCriteria = raw.AcceptanceCriteria.Count > 0
            ? SanitizeList(raw.AcceptanceCriteria)
            : [$"{(raw.Title ?? "").Trim()} meets acceptance within allowed mutation surface."],
        VerificationCommands = SanitizeList(raw.VerificationCommands),
        AllowedMutationSurface = SanitizeList(raw.AllowedMutationSurface),
        Status = JsdpNodeStatus.Pending,
        Outputs = SanitizeList(raw.Outputs ?? []),
        RepairOf = string.IsNullOrWhiteSpace(raw.RepairOf) ? null : raw.RepairOf.Trim(),
    };

    private static JsdpPlanValidationResult Finalize(JsdpPlanValidationResult result, int count = 0)
    {
        result.NodeCount = count;
        result.Valid = result.Errors.Count == 0;
        return result;
    }
}
