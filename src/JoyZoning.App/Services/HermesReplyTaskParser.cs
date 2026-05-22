using System.Text.RegularExpressions;

namespace JoyZoning.App.Services;

public record ParsedTaskSuggestion(string Title, string Description);

/// <summary>Extract actionable task lines from Hermes manager replies.</summary>
public static class HermesReplyTaskParser
{
    private static readonly Regex Checkbox = new(
        @"^[-*]\s*\[[ xX]?\]\s*(.+)$",
        RegexOptions.Compiled);

    private static readonly Regex Bullet = new(
        @"^[-*•]\s+(.+)$",
        RegexOptions.Compiled);

    private static readonly Regex Numbered = new(
        @"^\d+[.)]\s+(.+)$",
        RegexOptions.Compiled);

    public static IReadOnlyList<ParsedTaskSuggestion> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<ParsedTaskSuggestion>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ParsedTaskSuggestion>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length < 3) continue;

            string? title = null;
            if (Checkbox.IsMatch(line))
                title = Checkbox.Match(line).Groups[1].Value.Trim();
            else if (Numbered.IsMatch(line))
                title = Numbered.Match(line).Groups[1].Value.Trim();
            else if (Bullet.IsMatch(line))
                title = Bullet.Match(line).Groups[1].Value.Trim();

            if (title is null) continue;

            title = StripMarkdown(title);
            if (title.Length < 3 || title.Length > 200) continue;
            if (!seen.Add(title)) continue;

            results.Add(new ParsedTaskSuggestion(title, text.Trim()));
            if (results.Count >= 15) break;
        }

        return results;
    }

    private static string StripMarkdown(string s)
    {
        s = s.Trim('*', '_', '`', ' ', '"');
        if (s.StartsWith("**") && s.EndsWith("**") && s.Length > 4)
            s = s[2..^2];
        return s.Trim();
    }
}
