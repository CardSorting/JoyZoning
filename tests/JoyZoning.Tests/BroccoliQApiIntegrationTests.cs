using System.Net;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Collection("OrchestrationApi")]
[Trait(TestCategories.Key, TestCategories.Integration)]
public class BroccoliQApiIntegrationTests
{
    private readonly JoyZoningApiCollectionFixture _fixture;

    public BroccoliQApiIntegrationTests(JoyZoningApiCollectionFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Broccoliq_health_reports_disabled_in_testing_environment()
    {
        var client = _fixture.Client;
        var response = await client.GetAsync("/api/broccoliq/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"enabled\":false", json.Replace(" ", ""));
    }
}
