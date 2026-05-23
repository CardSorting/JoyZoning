using System.Text.RegularExpressions;

namespace JoyZoning.Agents.Hermes;

/// <summary>Minimal model/provider extraction from Hermes <c>config.yaml</c> (no full YAML parser).</summary>
public static class HermesConfigYamlReader
{
    public static HermesModelConfig ReadModel(string configYamlPath)
    {
        if (!File.Exists(configYamlPath))
            return new HermesModelConfig();

        var text = File.ReadAllText(configYamlPath);

        var inline = Regex.Match(
            text,
            @"^model:\s+['""]?([^'""#\r\n]+)['""]?\s*$",
            RegexOptions.Multiline);
        if (inline.Success)
        {
            var value = inline.Groups[1].Value.Trim();
            if (!value.Contains(':', StringComparison.Ordinal))
                return new HermesModelConfig { Model = value, Format = HermesModelFormat.InlineString };
        }

        var modelBlock = ExtractYamlBlock(text, "model");
        if (string.IsNullOrWhiteSpace(modelBlock))
            return new HermesModelConfig();

        var model = MatchScalar(modelBlock, "default")
            ?? MatchScalar(modelBlock, "name");
        var provider = MatchScalar(modelBlock, "provider");
        var baseUrl = MatchScalar(modelBlock, "base_url");

        return new HermesModelConfig
        {
            Model = model,
            Provider = provider,
            BaseUrl = baseUrl,
            Format = HermesModelFormat.Structured,
        };
    }

    private static string? ExtractYamlBlock(string yaml, string key)
    {
        var match = Regex.Match(
            yaml,
            $@"^{Regex.Escape(key)}:\s*\r?\n((?:[ \t].+(?:\r?\n|$))+)",
            RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? MatchScalar(string block, string key)
    {
        var m = Regex.Match(
            block,
            $@"^\s*{Regex.Escape(key)}:\s*['""]?([^'""#\r\n]+)['""]?\s*$",
            RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}

public enum HermesModelFormat
{
    Unknown,
    InlineString,
    Structured,
}

public sealed class HermesModelConfig
{
    public string? Model { get; init; }
    public string? Provider { get; init; }
    public string? BaseUrl { get; init; }
    public HermesModelFormat Format { get; init; }

    public string Describe()
    {
        if (Format == HermesModelFormat.InlineString && !string.IsNullOrWhiteSpace(Model))
            return Model;

        if (!string.IsNullOrWhiteSpace(Model) && !string.IsNullOrWhiteSpace(Provider))
            return $"{Provider}/{Model}";

        return Model ?? Provider ?? "(not set)";
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Model) || !string.IsNullOrWhiteSpace(Provider);
}
