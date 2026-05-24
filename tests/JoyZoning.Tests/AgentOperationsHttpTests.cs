using System.Net;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Trait("Category", "Unit")]
public sealed class AgentOperationsHttpTests
{
    [Fact]
    public async Task Agent_context_http_returns_runtime_state()
    {
        using var factory = new JoyZoningApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/agent/context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"manifestVersion\":\"1\"", json.Replace(" ", ""));
        Assert.Contains("/api/agent/manifest", json);
        Assert.Contains("pendingApprovals", json);
    }

    [Fact]
    public async Task Watch_bootstrap_includes_agent_ops()
    {
        using var factory = new JoyZoningApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/watch/bootstrap");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("agentOps", json);
        Assert.Contains("/api/agent/context", json);
    }

    [Fact]
    public async Task Agent_manifest_http_returns_manifest_version()
    {
        using var factory = new JoyZoningApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/agent/manifest");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"manifestVersion\":\"1\"", json.Replace(" ", ""));
        Assert.Contains("joyzoning", json);
    }

    [Fact]
    public async Task Agent_endpoints_http_can_filter_agent_safe()
    {
        using var factory = new JoyZoningApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/agent/endpoints?agentSafe=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"agentSafeOnly\":true", json.Replace(" ", ""));
        Assert.DoesNotContain("\"agentSafe\":false", json);
        Assert.Equal(JoyZoningEndpointRegistry.Endpoints.Count(e => e.AgentSafe),
            json.Split("\"id\":", StringSplitOptions.RemoveEmptyEntries).Length - 1);
    }
}
