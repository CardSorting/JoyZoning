using System.Net;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Tests.Infrastructure;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Integration)]
[Collection("OrchestrationApi")]
public class OrchestrationApiIntegrationTests : IAsyncLifetime
{
    private readonly JoyZoningApiCollectionFixture _fixture;
    private readonly OrchestrationApiClient _api;

    public OrchestrationApiIntegrationTests(JoyZoningApiCollectionFixture fixture)
    {
        _fixture = fixture;
        _api = new OrchestrationApiClient(fixture.Client);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string WorkspaceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static VerificationReport PassingReport(Guid cardId) => new()
    {
        CardId = cardId,
        SessionId = Guid.NewGuid(),
        CommandsRun = [new CommandRunSummary { Command = "dotnet build", Passed = true, Summary = "ok" }],
        ReadyForHumanReview = true,
    };

    private static VerificationReport FailingReport(Guid cardId) => new()
    {
        CardId = cardId,
        SessionId = Guid.NewGuid(),
        CommandsRun = [new CommandRunSummary { Command = "dotnet test", Passed = false, Summary = "failed" }],
        ReadyForHumanReview = false,
    };

    [Fact]
    public async Task CreateSession_reuses_existing_workspace()
    {
        var root = WorkspaceRoot();
        var id1 = await _api.CreateSessionAsync(root, "first");
        var id2 = await _api.CreateSessionAsync(root, "second");
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task CreateTask_reuses_existing_title_in_workspace()
    {
        var root = WorkspaceRoot();
        var (sessionId, taskId1) = await _api.SeedTaskAsync(root);
        var taskId2 = await _api.CreateTaskAsync(sessionId, "API test card", "duplicate title");
        Assert.Equal(taskId1, taskId2);
    }

    [Fact]
    public async Task Get_lease_returns_404_when_none()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        var (status, _, error, _) = await OrchestrationApiClient.ReadAsync(await _api.GetLeaseAsync(taskId));
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("lease_not_found", error);
    }

    [Fact]
    public async Task Dispatch_creates_lease_and_returns_202()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        var (status, body, error, message) = await OrchestrationApiClient.ReadAsync(await _api.DispatchAsync(taskId));
        var detail = body?.TryGetProperty("detail", out var d) == true ? d.GetString() : null;
        Assert.True(
            status == HttpStatusCode.Accepted,
            $"dispatch failed: status={status} error={error} message={message} detail={detail}");
        Assert.NotNull(body);

        var leaseResp = await _api.GetLeaseAsync(taskId);
        var (leaseStatus, leaseBody, _, _) = await OrchestrationApiClient.ReadAsync(leaseResp);
        Assert.Equal(HttpStatusCode.OK, leaseStatus);
        Assert.Equal((int)ExecutionLeaseStatus.Running, OrchestrationApiClient.LeaseStatus(leaseBody!.Value));
    }

    [Fact]
    public async Task Agent_status_blocked_requires_reason_400()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);

        var (status, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Blocked));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("lease_invalid_request", error);
        Assert.Contains("Reason", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Agent_status_invalid_target_400()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);

        var (status, _, error, _) = await OrchestrationApiClient.ReadAsync(
            await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.ReadyForReview));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("lease_invalid_request", error);
    }

    [Fact]
    public async Task Passing_verification_moves_to_ready_for_review()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Verifying);

        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.VerificationAsync(taskId, PassingReport(taskId)));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((int)ExecutionLeaseStatus.ReadyForReview, OrchestrationApiClient.LeaseStatus(body!.Value));
    }

    [Fact]
    public async Task Failed_verification_stays_verifying()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Verifying);

        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.VerificationAsync(taskId, FailingReport(taskId)));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((int)ExecutionLeaseStatus.Verifying, OrchestrationApiClient.LeaseStatus(body!.Value));
    }

    [Fact]
    public async Task Incomplete_verification_report_rejected_400()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Verifying);

        var incomplete = new VerificationReport
        {
            CardId = taskId,
            SessionId = Guid.NewGuid(),
            CommandsRun = [],
            ReadyForHumanReview = true,
        };

        var (status, _, error, _) = await OrchestrationApiClient.ReadAsync(
            await _api.VerificationAsync(taskId, incomplete));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("lease_invalid_request", error);
    }

    [Fact]
    public async Task Merge_requires_ready_for_review_409()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);

        var (status, _, error, _) = await OrchestrationApiClient.ReadAsync(await _api.MergeAsync(taskId));
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("lease_conflict", error);
    }

    [Fact]
    public async Task Merge_after_passing_verification_returns_200_and_completes_task()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Verifying);
        await _api.VerificationAsync(taskId, PassingReport(taskId));

        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(await _api.MergeAsync(taskId));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((int)WorkTaskStatus.Complete, body!.Value.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Revoke_returns_200_and_terminal_lease()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);

        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.RevokeAsync(taskId, "operator stop"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal((int)ExecutionLeaseStatus.Revoked, OrchestrationApiClient.LeaseStatus(body!.Value));
        Assert.Contains("lease.revoked", body.Value.GetProperty("evidenceLogJson").GetString());
    }

    [Fact]
    public async Task Terminal_lease_agent_status_409()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Verifying);
        await _api.VerificationAsync(taskId, PassingReport(taskId));
        await _api.MergeAsync(taskId);

        var (status, _, error, _) = await OrchestrationApiClient.ReadAsync(
            await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Blocked, "late"));

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("lease_not_found", error);
    }

    [Fact]
    public async Task DietCode_cannot_complete_task_403()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());

        var (status, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.UpdateTaskStatusAsync(taskId, WorkTaskStatus.Complete, StatusChangeActor.DietCode));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("lease_forbidden", error);
        Assert.Contains("human", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Critical_dispatch_without_approval_403()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot(), RiskLevel.Critical);

        var (status, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(taskId, humanApprovedCritical: false));

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("lease_forbidden", error);
        Assert.Contains("humanApprovedCritical", message!);
    }

    [Fact]
    public async Task Critical_dispatch_with_approval_succeeds()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot(), RiskLevel.Critical);

        var (status, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(taskId, humanApprovedCritical: true));

        Assert.True(
            status == HttpStatusCode.Accepted,
            $"critical dispatch failed: status={status} error={error} message={message}");
    }

    [Fact]
    public async Task Second_critical_dispatch_conflicts_409()
    {
        var ws = WorkspaceRoot();
        var (_, task1) = await _api.SeedTaskAsync(ws, RiskLevel.Critical);
        var (_, task2) = await _api.SeedTaskAsync(ws, RiskLevel.Critical);

        await _api.DispatchAsync(task1, humanApprovedCritical: true);

        var (status, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(task2, humanApprovedCritical: true));

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("lease_conflict", error);
        Assert.Contains("critical", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Critical_approval_consumed_after_running_dispatch()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot(), RiskLevel.Critical);
        await _api.DispatchAsync(taskId, humanApprovedCritical: true);

        var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await _api.GetLeaseAsync(taskId));
        Assert.True(
            lease!.Value.TryGetProperty("criticalApprovalConsumed", out var consumed) && consumed.GetBoolean());
    }

    [Fact]
    public async Task Dispatch_failure_leaves_blocked_lease_not_running()
    {
        _fixture.Factory.DietCode.FailNextDispatch = true;
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());

        var (dispatchStatus, _, error, _) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(taskId));

        Assert.Equal(HttpStatusCode.Conflict, dispatchStatus);
        Assert.Equal("lease_conflict", error);

        var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await _api.GetLeaseAsync(taskId));
        Assert.Equal((int)ExecutionLeaseStatus.Blocked, OrchestrationApiClient.LeaseStatus(lease!.Value));
        Assert.Contains("dispatch.failed", lease.Value.GetProperty("evidenceLogJson").GetString());
    }

    [Fact]
    public async Task Supersede_false_blocks_weaker_replacement_409()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Verifying);
        await LeaseTestSeed.SetVerifyingWithPassingReportAsync(_fixture.Factory.Services, taskId, PassingReport(taskId));

        var replacement = new VerificationReport
        {
            CardId = taskId,
            SessionId = Guid.NewGuid(),
            CommandsRun = [new CommandRunSummary { Command = "dotnet test", Passed = true, Summary = "ok" }],
            ReadyForHumanReview = true,
        };

        var (status, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.VerificationAsync(taskId, replacement, supersede: false));

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("lease_conflict", error);
        Assert.Contains("supersede", message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Supersede_true_records_superseded_evidence()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Verifying);
        await LeaseTestSeed.SetVerifyingWithPassingReportAsync(_fixture.Factory.Services, taskId, PassingReport(taskId));

        var replacement = new VerificationReport
        {
            CardId = taskId,
            SessionId = Guid.NewGuid(),
            CommandsRun =
            [
                new CommandRunSummary { Command = "dotnet build", Passed = true, Summary = "ok" },
                new CommandRunSummary { Command = "dotnet test", Passed = true, Summary = "ok" },
            ],
            ReadyForHumanReview = true,
        };

        var (status, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.VerificationAsync(taskId, replacement, supersede: true));

        Assert.Equal(HttpStatusCode.OK, status);
        var evidence = body!.Value.GetProperty("evidenceLogJson").GetString() ?? "";
        Assert.Contains("verification.superseded", evidence);
    }

    [Fact]
    public async Task Dispatch_unknown_task_404()
    {
        var (status, _, error, _) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("task_not_found", error);
    }

    [Fact]
    public async Task Second_dispatch_same_card_conflicts_409()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);

        var (status, _, error, message) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(taskId));

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("lease_conflict", error);
        Assert.Contains("active", message!, StringComparison.OrdinalIgnoreCase);
    }
}
