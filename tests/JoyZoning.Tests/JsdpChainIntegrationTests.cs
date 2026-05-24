using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Integration)]
[Collection("OrchestrationApi")]
public class JsdpChainIntegrationTests : IAsyncLifetime
{
    private readonly JoyZoningApiCollectionFixture _fixture;
    private readonly OrchestrationApiClient _api;

    public JsdpChainIntegrationTests(JoyZoningApiCollectionFixture fixture)
    {
        _fixture = fixture;
        _api = new OrchestrationApiClient(fixture.Client);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string WorkspaceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-jsdp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        GitTestHelper.InitRepo(root);
        return root;
    }

    private static VerificationReport JsdpPassingReport(Guid cardId) =>
        new()
        {
            CardId = cardId,
            SessionId = Guid.NewGuid(),
            CommandsRun =
            [
                new CommandRunSummary
                {
                    Command = "dotnet build",
                    Passed = true,
                    Summary = string.Join("\n", JsdpProtocol.RequiredOutputSections),
                },
            ],
            ReadyForHumanReview = true,
        };

    [Fact]
    public async Task Create_chain_blocks_role_2_until_role_1_accept_merged()
    {
        var root = WorkspaceRoot();
        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.CreateDeliveryChainAsync("JSDP IT", root));
        Assert.Equal(HttpStatusCode.Created, status);

        var chainId = Guid.Parse(body!.Value.GetProperty("chainId").GetString()!);
        Assert.Equal(JsdpProtocol.ProtocolId, body.Value.GetProperty("protocol").GetString());

        var members = body.Value.GetProperty("members");
        Assert.Equal(8, members.GetArrayLength());
        var role1Task = Guid.Parse(members[0].GetProperty("taskId").GetString()!);
        var role2Task = Guid.Parse(members[1].GetProperty("taskId").GetString()!);
        var role2Session = Guid.Parse(members[1].GetProperty("sessionId").GetString()!);

        var (queueStatus, queueBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetDeliveryChainQueueAsync(chainId));
        Assert.Equal(HttpStatusCode.OK, queueStatus);
        Assert.Equal("eligible", queueBody!.Value.GetProperty("jsdp").GetProperty("nextDispatchEligibility").GetString());

        var role2Step = queueBody.Value.GetProperty("steps")[1];
        Assert.False(role2Step.GetProperty("dispatchable").GetBoolean());
        Assert.Contains("merge gate closed", role2Step.GetProperty("blockReason").GetString(), StringComparison.OrdinalIgnoreCase);

        var (dispatchBlockedStatus, _, dispatchError, dispatchMessage) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(role2Task));
        Assert.Equal(HttpStatusCode.Conflict, dispatchBlockedStatus);
        Assert.Equal("lease_conflict", dispatchError);
        Assert.Contains("merge gate closed", dispatchMessage!, StringComparison.OrdinalIgnoreCase);

        var (completeBypassStatus, _, completeError, completeMessage) = await OrchestrationApiClient.ReadAsync(
            await _api.UpdateTaskStatusAsync(role1Task, WorkTaskStatus.Complete));
        Assert.Equal(HttpStatusCode.Forbidden, completeBypassStatus);
        Assert.Equal("lease_forbidden", completeError);
        Assert.Contains("jsdp_convergence_required", completeMessage!, StringComparison.OrdinalIgnoreCase);

        await _api.DispatchAsync(role1Task);
        await _api.AgentStatusAsync(role1Task, ExecutionLeaseStatus.Verifying);
        await _api.VerificationAsync(role1Task, JsdpPassingReport(role1Task));
        var (mergeStatus, mergeBody, mergeError, mergeMessage) = await OrchestrationApiClient.ReadAsync(
            await _api.MergeAsync(role1Task));
        Assert.Equal(HttpStatusCode.OK, mergeStatus);

        var (queueAfterStatus, queueAfterBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetDeliveryChainQueueAsync(chainId));
        Assert.Equal(HttpStatusCode.OK, queueAfterStatus);
        var eligibility = queueAfterBody!.Value.GetProperty("jsdp").GetProperty("nextDispatchEligibility").GetString();
        var role2Block = queueAfterBody.Value.GetProperty("steps")[1].GetProperty("blockReason").GetString();
        Assert.Equal("eligible", eligibility);
        Assert.True(string.IsNullOrEmpty(role2Block), role2Block);
        Assert.True(queueAfterBody.Value.GetProperty("steps")[1].GetProperty("dispatchable").GetBoolean());

        var planResp = await _fixture.Client.GetAsync($"api/sessions/{role2Session}/delivery-plan");
        planResp.EnsureSuccessStatusCode();
        var plan = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("eligible", plan.GetProperty("jsdp").GetProperty("nextDispatchEligibility").GetString());
    }
}
