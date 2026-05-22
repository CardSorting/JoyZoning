using System.Text.RegularExpressions;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Extracts task tags from title/description (no separate tag entity in JoyZoning).</summary>
public static partial class YoloTaskTags
{
    private static readonly Regex TagsLine = TagsLineRegex();
    private static readonly Regex HashTag = HashTagRegex();

    public static IReadOnlyList<string> Extract(string title, string? description)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = $"{title}\n{description ?? ""}";

        foreach (Match m in HashTag.Matches(text))
        {
            var tag = m.Groups[1].Value.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(tag))
                tags.Add(tag);
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = trimmed["tags:".Length..];
            foreach (var part in payload.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                var tag = part.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(tag))
                    tags.Add(tag);
            }
        }

        return tags.OrderBy(t => t, StringComparer.Ordinal).ToList();
    }

    public static bool MatchesPolicy(
        IReadOnlyList<string> taskTags,
        IReadOnlyList<string> allowedTaskTags,
        IReadOnlyList<string> forbiddenTaskTags,
        out string? reason)
    {
        var allowed = NormalizeList(allowedTaskTags);
        var forbidden = NormalizeList(forbiddenTaskTags);

        foreach (var f in forbidden)
        {
            if (taskTags.Any(t => t.Equals(f, StringComparison.OrdinalIgnoreCase)))
            {
                reason = $"Task has forbidden tag '{f}'.";
                return false;
            }
        }

        if (allowed.Count > 0 && !taskTags.Any(t => allowed.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            reason = $"Task lacks any allowed tag (need one of: {string.Join(", ", allowed)}).";
            return false;
        }

        reason = null;
        return true;
    }

    private static List<string> NormalizeList(IReadOnlyList<string> tags) =>
        tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    [GeneratedRegex(@"#([\w][\w\-]*)", RegexOptions.IgnoreCase)]
    private static partial Regex HashTagRegex();

    [GeneratedRegex(@"^tags:\s*", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TagsLineRegex();
}
