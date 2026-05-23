using System.Text.RegularExpressions;

namespace JoyZoning.Domain.Orchestration;

/// <summary>Classifies changed paths into protected categories (auth, payments, infra, …).</summary>
public static partial class ProtectedPathMatcher
{
    private static readonly (string Category, string[] Patterns)[] Rules =
    [
        ("auth", ["**/auth/**", "**/authentication/**", "**/authorization/**", "**/identity/**", "**/login/**", "**/oauth/**"]),
        ("payments", ["**/payment/**", "**/payments/**", "**/billing/**", "**/stripe/**", "**/checkout/**"]),
        ("admin", ["**/admin/**", "**/superuser/**"]),
        ("migrations", ["**/migrations/**", "**/migration/**", "**/*migration*"]),
        ("database_schema", ["**/schema/**", "**/efcore/**", "**/*.sql"]),
        ("secrets", ["**/.env*", "**/secrets/**", "**/*secret*", "**/appsettings*.json", "**/credentials*"]),
        ("infrastructure", ["**/infra/**", "**/infrastructure/**", "**/deploy/**", "**/deployment/**", "**/k8s/**", "**/terraform/**", "**/helm/**"]),
        ("dependencies", ["**/package.json", "**/package-lock.json", "**/pnpm-lock.yaml", "**/yarn.lock", "**/*.csproj", "**/Directory.Packages.props", "**/requirements.txt", "**/go.mod", "**/Cargo.toml"]),
        ("cicd", ["**/.github/**", "**/gitlab-ci*", "**/azure-pipelines*", "**/Jenkinsfile", "**/.circleci/**"]),
        ("security", ["**/security/**", "**/*security*", "**/firewall/**"]),
    ];

    public static IReadOnlyList<(string Path, string Category)> FindProtected(IEnumerable<string> paths)
    {
        var hits = new List<(string, string)>();
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var normalized = path.Replace('\\', '/').TrimStart('/');
            foreach (var (category, patterns) in Rules)
            {
                if (patterns.Any(p => GlobMatches(normalized, p)))
                {
                    hits.Add((path, category));
                    break;
                }
            }
        }

        return hits;
    }

    public static bool GlobMatches(string path, string pattern)
    {
        var normalizedPattern = pattern.Replace('\\', '/').TrimStart('/');
        if (normalizedPattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var prefix = normalizedPattern[..^3];
            if (path.Equals(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var regex = "^" + Regex.Escape(normalizedPattern)
            .Replace(@"\*\*/", "(.*/)?")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", ".") + "$";
        return Regex.IsMatch(path, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool MatchesAnyGlob(string path, IEnumerable<string> globs) =>
        globs.Any(g => GlobMatches(path.Replace('\\', '/').TrimStart('/'), g));
}
