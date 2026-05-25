using System.Text.RegularExpressions;
using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class ProjectSpecAnalyzer
{
    public ProjectSpecAnalysis Analyze(ProjectSpecDocument spec)
    {
        var text = spec.RawMarkdown ?? spec.Goal;
        var lines = text.Split('\n', StringSplitOptions.None);

        return new ProjectSpecAnalysis
        {
            ProductGoal = ExtractProductGoal(spec.Goal, lines),
            TechStack = ExtractSectionList(lines, "tech stack", "technology", "stack"),
            DomainConcepts = ExtractDomainConcepts(lines),
            CoreSystems = ExtractSectionList(lines, "core systems", "systems", "architecture", "components"),
            Constraints = ExtractSectionList(lines, "constraints", "requirements", "must"),
            NonGoals = ExtractSectionList(lines, "non-goals", "non goals", "out of scope", "not"),
            AcceptanceCriteria = ExtractSectionList(lines, "acceptance", "success criteria", "done when"),
            VerificationStrategy = ExtractSectionList(lines, "verification", "testing", "validate", "quality"),
        };
    }

    private static string ExtractProductGoal(string fallbackGoal, string[] lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal) && !trimmed.StartsWith("## ", StringComparison.Ordinal))
                return trimmed[2..].Trim();
        }

        var goalLine = lines.FirstOrDefault(l =>
            l.Contains("goal", StringComparison.OrdinalIgnoreCase) &&
            l.TrimStart().StartsWith('-'));
        if (goalLine is not null)
            return goalLine.Trim().TrimStart('-').Trim();

        return fallbackGoal.Trim();
    }

    private static List<string> ExtractSectionList(string[] lines, params string[] sectionKeywords)
    {
        var result = new List<string>();
        var inSection = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('#'))
            {
                var heading = line.TrimStart('#').Trim().ToLowerInvariant();
                inSection = sectionKeywords.Any(k => heading.Contains(k, StringComparison.Ordinal));
                continue;
            }

            if (!inSection)
                continue;

            if (line.StartsWith('#'))
                break;

            if (line.StartsWith('-') || line.StartsWith('*') || Regex.IsMatch(line, @"^\d+\."))
            {
                var item = line.TrimStart('-', '*', ' ').Trim();
                if (item.Length > 0 && !item.StartsWith('#'))
                    result.Add(item);
            }
            else if (line.Length > 0 && !line.StartsWith("```") && result.Count == 0 && line.Length < 200)
            {
                result.Add(line);
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToList();
    }

    private static List<string> ExtractDomainConcepts(string[] lines)
    {
        var concepts = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                var heading = trimmed[3..].Trim();
                if (!IsMetaHeading(heading))
                    concepts.Add(heading);
            }

            var backtickTerms = Regex.Matches(trimmed, @"`([^`]+)`")
                .Select(m => m.Groups[1].Value)
                .Where(t => t.Length is > 2 and < 48);
            concepts.AddRange(backtickTerms);
        }

        return concepts.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
    }

    private static bool IsMetaHeading(string heading)
    {
        var lower = heading.ToLowerInvariant();
        return lower is "overview" or "introduction" or "table of contents" or "summary"
            || lower.Contains("acceptance")
            || lower.Contains("verification")
            || lower.Contains("constraint");
    }
}
