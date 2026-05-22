using System.Text.Json;

namespace JoyZoning.Cli;

public static class CliJson
{
    public static string? ExtractField(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var el = root;
        foreach (var segment in path.Trim().TrimStart('.').Split('.'))
        {
            if (string.IsNullOrEmpty(segment))
                continue;

            if (!TryGetProperty(el, segment, out el))
                return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => el.GetRawText(),
        };
    }

    private static bool TryGetProperty(JsonElement el, string name, out JsonElement value)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (el.TryGetProperty(name, out value))
            return true;

        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(camel, out value))
            return true;

        return false;
    }
}
