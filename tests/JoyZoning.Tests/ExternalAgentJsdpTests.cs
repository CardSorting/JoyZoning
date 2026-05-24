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
public class ExternalAgentJsdpTests : IAsyncLifetime
{
    private readonly JoyZoningApiCollectionFixture _fixture;
    private readonly OrchestrationApiClient _api;

    public ExternalAgentJsdpTests(JoyZoningApiCollectionFixture fixture)
    {
        _fixture = fixture;
        _api = new OrchestrationApiClient(fixture.Client);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string WorkspaceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        GitTestHelper.InitRepo(root);
        return root;
    }

  [Fact]
    public async Task Start_external_task_does_not_create_hermes_lease()
    {
        var root = WorkspaceRoot();
        var (_, taskId) = await _api.SeedTaskAsync(root);

        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.StartExternalAsync(taskId, "cursor"));
        Assert.Equal(HttpStatusCode.OK, status);

        var branch = body!.Value.GetProperty("branchName").GetString();
        Assert.Contains("joyzoning/card-", branch, StringComparison.Ordinal);

        var (leaseStatus, _, leaseError, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetLeaseAsync(taskId));
        Assert.Equal(HttpStatusCode.NotFound, leaseStatus);
        Assert.Equal("lease_not_found", leaseError);

        Assert.Equal((int)WorkTaskStatus.ExternalInProgress, body.Value.GetProperty("status").GetInt32());
        Assert.Equal((int)TaskExecutionMode.ExternalAgent, body.Value.GetProperty("taskExecutionMode").GetInt32());
        Assert.Equal((int)ExecutionDriver.ExternalCursor, body.Value.GetProperty("executionDriver").GetInt32());
    }

    [Fact]
    public async Task External_prompt_includes_jsdp_rules_and_role_scope()
    {
        var root = WorkspaceRoot();
        var (_, taskId) = await _api.SeedTaskAsync(root);
        await _api.StartExternalAsync(taskId, "cursor");

        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetExternalPromptAsync(taskId));
        Assert.Equal(HttpStatusCode.OK, status);
        var prompt = body!.Value.GetProperty("prompt").GetString()!;
        Assert.Contains("JSDP", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Required output", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API test card", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mark_ready_blocked_without_changes_or_branch_mismatch()
    {
        var root = WorkspaceRoot();
        var (_, taskId) = await _api.SeedTaskAsync(root);
        await _api.StartExternalAsync(taskId, "manual");

        var (blockedStatus, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.MarkExternalReadyAsync(taskId));
        Assert.Equal(HttpStatusCode.Conflict, blockedStatus);
        Assert.Equal("external_task_conflict", error);
        Assert.Contains("No workspace changes", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mark_ready_succeeds_with_changes_and_complete_gate()
    {
        var root = WorkspaceRoot();
        var (_, taskId) = await _api.SeedTaskAsync(root);
        await _api.StartExternalAsync(taskId, "cursor");

        await File.WriteAllTextAsync(Path.Combine(root, "external-touch.txt"), "jz external");

        var (readyStatus, _, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.MarkExternalReadyAsync(taskId));
        Assert.Equal(HttpStatusCode.OK, readyStatus);

        GitTestHelper.CommitAll(root, "external-touch");

        var report = ExternalPassingReport(taskId);
        var (verifyStatus, _, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.VerificationAsync(taskId, report));
        Assert.Equal(HttpStatusCode.OK, verifyStatus);

        var (completeBlockedStatus, _, completeError, _) = await OrchestrationApiClient.ReadAsync(
            await _api.UpdateTaskStatusAsync(taskId, WorkTaskStatus.Complete));
        Assert.Equal(HttpStatusCode.Forbidden, completeBlockedStatus);
        Assert.Equal("external_task_forbidden", completeError);

        var (mergeStatus, _, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.CompleteExternalAsync(taskId));
        Assert.Equal(HttpStatusCode.OK, mergeStatus);

        var (taskStatus, taskBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetTaskAsync(taskId));
        Assert.Equal(HttpStatusCode.OK, taskStatus);
        Assert.Equal((int)WorkTaskStatus.Complete, taskBody!.Value.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Delivery_chain_external_next_blocks_role_2_until_role_1_complete()
    {
        var root = WorkspaceRoot();
        var (createStatus, createBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.CreateDeliveryChainAsync("External JSDP IT", root));
        Assert.Equal(HttpStatusCode.Created, createStatus);

        var chainId = Guid.Parse(createBody!.Value.GetProperty("chainId").GetString()!);
        var role1Task = Guid.Parse(createBody.Value.GetProperty("members")[0].GetProperty("taskId").GetString()!);
        var role2Task = Guid.Parse(createBody.Value.GetProperty("members")[1].GetProperty("taskId").GetString()!);

        var (nextStatus, nextBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.DeliveryChainNextExternalAsync(chainId, "cursor"));
        Assert.Equal(HttpStatusCode.OK, nextStatus);
        Assert.Equal(role1Task.ToString(), nextBody!.Value.GetProperty("taskId").GetString());

        var (leaseStatus, _, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetLeaseAsync(role1Task));
        Assert.Equal(HttpStatusCode.NotFound, leaseStatus);

        var (queueStatus, queueBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetDeliveryChainQueueAsync(chainId));
        Assert.Equal(HttpStatusCode.OK, queueStatus);
        Assert.False(queueBody!.Value.GetProperty("steps")[1].GetProperty("dispatchable").GetBoolean());

        await File.WriteAllTextAsync(Path.Combine(root, "role1.txt"), "role 1");
        await _api.MarkExternalReadyAsync(role1Task);
        GitTestHelper.CommitAll(root, "role1");
        await _api.VerificationAsync(role1Task, ExternalPassingReport(role1Task));
        await _api.CompleteExternalAsync(role1Task);

        var (queueAfterStatus, queueAfterBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.GetDeliveryChainQueueAsync(chainId));
        Assert.Equal(HttpStatusCode.OK, queueAfterStatus);
        Assert.True(queueAfterBody!.Value.GetProperty("steps")[1].GetProperty("dispatchable").GetBoolean());

        var (role2NextStatus, role2NextBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.DeliveryChainNextExternalAsync(chainId, "cursor"));
        Assert.Equal(HttpStatusCode.OK, role2NextStatus);
        Assert.Equal(role2Task.ToString(), role2NextBody!.Value.GetProperty("taskId").GetString());
    }

    [Fact]
    public async Task Workspace_status_endpoint_reports_driver_and_scan()
    {
        var root = WorkspaceRoot();
        var (_, taskId) = await _api.SeedTaskAsync(root);
        await _api.StartExternalAsync(taskId, "cursor");
        await File.WriteAllTextAsync(Path.Combine(root, "scan-me.txt"), "x");

        var resp = await _fixture.Client.GetAsync($"api/tasks/{taskId}/workspace/status");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)ExecutionDriver.ExternalCursor, json.GetProperty("executionDriver").GetInt32());
        Assert.Equal((int)TaskExecutionMode.ExternalAgent, json.GetProperty("taskExecutionMode").GetInt32());
        Assert.True(json.GetProperty("hasChanges").GetBoolean());
    }

    private static VerificationReport ExternalPassingReport(Guid cardId) =>
        new()
        {
            CardId = cardId,
            SessionId = Guid.NewGuid(),
            CommandsRun =
            [
                new CommandRunSummary
                {
                    Command = "echo ok",
                    Passed = true,
                    Summary = string.Join("\n", JsdpProtocol.RequiredOutputSections),
                },
            ],
            ReadyForHumanReview = true,
        };
}
