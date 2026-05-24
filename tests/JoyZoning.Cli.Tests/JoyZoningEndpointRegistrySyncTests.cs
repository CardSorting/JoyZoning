using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Cli.Tests;

public sealed class JoyZoningEndpointRegistrySyncTests
{
    [Fact]
    public void Registry_matches_ApiEndpoints_source()
    {
        var root = FindRepoRoot();
        var report = JoyZoningEndpointRegistrySync.CompareRegistryToApiFile(root);
        Assert.True(report.Ok, JoyZoningEndpointRegistrySync.FormatDiff(report));
    }

    [Fact]
    public void ParseApiEndpointsSource_normalizes_route_constraints()
    {
        const string source = """
            app.MapGet("/api/tasks/{id:guid}/lease", async () => Results.Ok());
            app.MapPost("/api/sessions", async () => Results.Ok());
            """;

        var routes = JoyZoningEndpointRegistrySync.ParseApiEndpointsSource(source);
        Assert.Contains(new EndpointRouteKey("GET", "/api/tasks/{id}/lease"), routes);
        Assert.Contains(new EndpointRouteKey("POST", "/api/sessions"), routes);
    }

    [Fact]
    public void ParseRegistryRoutes_strips_query_parameters()
    {
        var routes = JoyZoningEndpointRegistrySync.ParseRegistryRoutes();
        Assert.Contains(new EndpointRouteKey("GET", "/api/tasks"), routes);
        Assert.DoesNotContain(routes, r => r.Path.Contains('?', StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        for (var current = dir; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "JoyZoning.sln")))
                return current.FullName;
        }

        throw new InvalidOperationException("JoyZoning.sln not found from test working directory.");
    }
}
