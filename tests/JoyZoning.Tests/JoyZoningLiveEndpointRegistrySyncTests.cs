using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Trait("Category", "Unit")]
public sealed class JoyZoningLiveEndpointRegistrySyncTests
{
    [Fact]
    public void Live_mapped_api_routes_are_documented_in_registry()
    {
        using var factory = new JoyZoningApiFactory();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var liveRoutes = new HashSet<EndpointRouteKey>();

        foreach (var endpoint in dataSource.Endpoints)
        {
            if (endpoint is not RouteEndpoint routeEndpoint)
                continue;

            var rawPath = routeEndpoint.RoutePattern.RawText;
            if (string.IsNullOrEmpty(rawPath) || !rawPath.StartsWith("/api/", StringComparison.Ordinal))
                continue;

            var path = NormalizeLivePath(rawPath);
            if (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>() is { HttpMethods.Count: > 0 } methods)
            {
                foreach (var method in methods.HttpMethods)
                    liveRoutes.Add(new EndpointRouteKey(method, path));
            }
            else
            {
                liveRoutes.Add(new EndpointRouteKey("GET", path));
            }
        }

        var registry = JoyZoningEndpointRegistrySync.ParseRegistryRoutes();
        var missing = liveRoutes.Except(registry).OrderBy(r => r.Path).ThenBy(r => r.Method).ToList();
        var extra = registry.Except(liveRoutes).OrderBy(r => r.Path).ThenBy(r => r.Method).ToList();

        Assert.True(missing.Count == 0,
            "Missing from registry: " + string.Join(", ", missing.Select(r => $"{r.Method} {r.Path}")));
        Assert.True(extra.Count == 0,
            "Extra in registry: " + string.Join(", ", extra.Select(r => $"{r.Method} {r.Path}")));
    }

    private static string NormalizeLivePath(string path) =>
        Regex.Replace(path, @":\w+", "", RegexOptions.CultureInvariant);
}
