using System.Text.RegularExpressions;

namespace JoyZoning.Domain.Orchestration;

public sealed record EndpointRouteKey(string Method, string Path);

public sealed record EndpointSyncReport(
    bool Ok,
    IReadOnlyList<EndpointRouteKey> MissingFromRegistry,
    IReadOnlyList<EndpointRouteKey> ExtraInRegistry,
    int ApiRouteCount,
    int RegistryRouteCount);

/// <summary>
/// Keeps the typed agent endpoint registry aligned with ApiEndpoints.cs.
/// </summary>
public static class JoyZoningEndpointRegistrySync
{
    private static readonly Regex MapRouteRegex = new(
        @"app\.Map(Get|Post|Put|Delete|Patch)\(""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public const string ApiEndpointsRelativePath =
        "src/JoyZoning.ControlPlane/Endpoints/ApiEndpoints.cs";

    public static EndpointSyncReport CompareRegistryToApiSource(string apiEndpointsSource)
    {
        var apiRoutes = ParseApiEndpointsSource(apiEndpointsSource);
        var registryRoutes = ParseRegistryRoutes();
        return BuildReport(apiRoutes, registryRoutes);
    }

    public static EndpointSyncReport CompareRegistryToApiFile(string workspaceRoot)
    {
        var path = Path.Combine(workspaceRoot, ApiEndpointsRelativePath);
        if (!File.Exists(path))
        {
            return new EndpointSyncReport(
                false,
                [],
                [],
                0,
                ParseRegistryRoutes().Count);
        }

        return CompareRegistryToApiSource(File.ReadAllText(path));
    }

    public static IReadOnlySet<EndpointRouteKey> ParseApiEndpointsSource(string source)
    {
        var routes = new HashSet<EndpointRouteKey>();
        foreach (Match match in MapRouteRegex.Matches(source))
        {
            var method = match.Groups[1].Value.ToUpperInvariant();
            var path = NormalizePath(match.Groups[2].Value);
            routes.Add(new EndpointRouteKey(method, path));
        }

        return routes;
    }

    public static IReadOnlySet<EndpointRouteKey> ParseRegistryRoutes()
    {
        var routes = new HashSet<EndpointRouteKey>();
        foreach (var endpoint in JoyZoningEndpointRegistry.Endpoints)
            routes.Add(new EndpointRouteKey(endpoint.Method.ToUpperInvariant(), NormalizeRegistryPath(endpoint.Path)));
        return routes;
    }

    public static EndpointSyncReport BuildReport(
        IReadOnlySet<EndpointRouteKey> apiRoutes,
        IReadOnlySet<EndpointRouteKey> registryRoutes)
    {
        var missing = apiRoutes.Except(registryRoutes).OrderBy(r => r.Path).ThenBy(r => r.Method).ToList();
        var extra = registryRoutes.Except(apiRoutes).OrderBy(r => r.Path).ThenBy(r => r.Method).ToList();
        return new EndpointSyncReport(
            missing.Count == 0 && extra.Count == 0,
            missing,
            extra,
            apiRoutes.Count,
            registryRoutes.Count);
    }

    public static string FormatDiff(EndpointSyncReport report)
    {
        if (report.Ok)
            return $"Endpoint registry matches ApiEndpoints ({report.ApiRouteCount} routes).";

        var parts = new List<string>();
        if (report.MissingFromRegistry.Count > 0)
            parts.Add("Missing from registry: " + string.Join(", ", report.MissingFromRegistry.Select(r => $"{r.Method} {r.Path}")));
        if (report.ExtraInRegistry.Count > 0)
            parts.Add("Extra in registry: " + string.Join(", ", report.ExtraInRegistry.Select(r => $"{r.Method} {r.Path}")));
        return string.Join("; ", parts);
    }

    private static string NormalizePath(string path) =>
        Regex.Replace(path, @":\w+", "", RegexOptions.CultureInvariant);

    private static string NormalizeRegistryPath(string path)
    {
        var queryIndex = path.IndexOf('?', StringComparison.Ordinal);
        return queryIndex >= 0 ? path[..queryIndex] : path;
    }
}
