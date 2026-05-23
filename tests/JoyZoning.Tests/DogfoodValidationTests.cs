using System.Net;
using System.Text.Json;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using JoyZoning.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JoyZoning.Tests;

/// <summary>Phase 27 end-to-end dogfood paths (API + jz CLI against test control plane).</summary>
[Trait(TestCategories.Key, TestCategories.Dogfood)]
[Collection("DogfoodApi")]
public class DogfoodValidationTests : IClassFixture<JoyZoningDogfoodApiFixture>, IAsyncLifetime
{
    private readonly JoyZoningDogfoodApiFixture _fixture;
    private readonly OrchestrationApiClient _api;
    private string? _jzBaseUrl;

    public DogfoodValidationTests(JoyZoningDogfoodApiFixture fixture)
    {
        _fixture = fixture;
        _api = new OrchestrationApiClient(fixture.Client);
    }

    private string JzBaseUrl => (_jzBaseUrl ??= _fixture.PublicBaseUrl).TrimEnd('/');

    private OrchestrationApiClient JzApi =>
        new(new HttpClient { BaseAddress = new Uri(JzBaseUrl + "/") });

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string WorkspaceRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-dogfood-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static VerificationReport PassingReport(Guid cardId, Guid sessionId) => new()
    {
        CardId = cardId,
        SessionId = sessionId,
        CommandsRun = [new CommandRunSummary { Command = "true", Passed = true, Summary = "ok" }],
        ReadyForHumanReview = true,
    };

    [Fact]
    public async Task Dogfood_01_happy_path_api_and_jz_agent()
    {
        var api = JzApi;
        var (sessionId, taskId) = await api.SeedTaskAsync(WorkspaceRoot(), RiskLevel.Low);
        await api.DispatchAsync(taskId);

        var (_, leaseStart, _, _) = await OrchestrationApiClient.ReadAsync(await api.GetLeaseAsync(taskId));
        var worktree = leaseStart!.Value.GetProperty("worktreePath").GetString()!;

        var (startCode, _, startErr) = await DogfoodCliRunner.RunInWorktreeAsync(
            JzBaseUrl, worktree, "agent", "start", "--task", taskId.ToString());
        Assert.True(startCode == 0, $"agent start failed (exit {startCode}): {startErr}");

        var ctxPath = Path.Combine(worktree, ".joyzoning", "context.json");
        Assert.True(File.Exists(ctxPath));

        var (hbCode, _, hbErr) = await DogfoodCliRunner.RunInWorktreeAsync(JzBaseUrl, worktree, "agent", "heartbeat");
        Assert.True(hbCode == 0, $"agent heartbeat failed (exit {hbCode}): {hbErr}");

        var verifyCmd = OperatingSystem.IsWindows() ? "exit /b 0" : "true";
        var (vCode, _, vErr) = await DogfoodCliRunner.RunInWorktreeAsync(
            JzBaseUrl, worktree, "agent", "verify", "--cmd", verifyCmd);
        Assert.True(vCode == 0, $"agent verify failed (exit {vCode}): {vErr}");

        var (doneCode, _, doneErr) = await DogfoodCliRunner.RunInWorktreeAsync(JzBaseUrl, worktree, "agent", "done");
        Assert.True(doneCode == 0, $"agent done failed (exit {doneCode}): {doneErr}");

        var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await api.GetLeaseAsync(taskId));
        Assert.Equal((int)ExecutionLeaseStatus.ReadyForReview, OrchestrationApiClient.LeaseStatus(lease!.Value));

        var (mergeCode, mergeBody, _, _) = await OrchestrationApiClient.ReadAsync(
            await api.MergeAsync(taskId));
        Assert.Equal(HttpStatusCode.OK, mergeCode);
        Assert.Equal((int)WorkTaskStatus.Complete, mergeBody!.Value.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Dogfood_02_verification_failure_blocks_done_and_complete()
    {
        var api = JzApi;
        var (sessionId, taskId) = await api.SeedTaskAsync(WorkspaceRoot());
        await api.DispatchAsync(taskId);
        var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await api.GetLeaseAsync(taskId));
        var worktree = lease!.Value.GetProperty("worktreePath").GetString()!;

        var (startCode, _, startErr) = await DogfoodCliRunner.RunInWorktreeAsync(
            JzBaseUrl, worktree, "agent", "start", "--task", taskId.ToString());
        Assert.True(startCode == 0, $"agent start failed (exit {startCode}): {startErr}");

        var failCmd = OperatingSystem.IsWindows() ? "exit /b 1" : "false";
        var (vCode, _, vErr) = await DogfoodCliRunner.RunInWorktreeAsync(
            JzBaseUrl, worktree, "agent", "verify", "--cmd", failCmd);
        Assert.True(vCode == 1, $"agent verify expected exit 1, got {vCode}: {vErr}");

        var (doneCode, _, _) = await DogfoodCliRunner.RunInWorktreeAsync(JzBaseUrl, worktree, "agent", "done");
        Assert.NotEqual(0, doneCode);

        var (_, leaseAfter, _, _) = await OrchestrationApiClient.ReadAsync(await api.GetLeaseAsync(taskId));
        Assert.NotEqual((int)ExecutionLeaseStatus.ReadyForReview, OrchestrationApiClient.LeaseStatus(leaseAfter!.Value));
    }

    [Fact]
    public async Task Dogfood_03_critical_dispatch_and_retry_approval()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot(), RiskLevel.Critical);

        var (noApproval, _, error, _) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchAsync(taskId, humanApprovedCritical: false));
        Assert.Equal(HttpStatusCode.Forbidden, noApproval);
        Assert.Equal("lease_forbidden", error);

        await _api.DispatchAsync(taskId, humanApprovedCritical: true);

        await _api.AgentStatusAsync(taskId, ExecutionLeaseStatus.Blocked, "test block");

        var (_, leaseBody, _, _) = await OrchestrationApiClient.ReadAsync(await _api.GetLeaseAsync(taskId));
        var sessionId2 = Guid.Parse(leaseBody!.Value.GetProperty("assignedSessionId").GetString()!);

        await _api.RecoverAsync(taskId, sessionId2, LeaseRecoveryMode.ReopenBlocked, humanApprovedCritical: true);

        var (retryFail, _, retryErr, retryMsg) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchRetryAsync(taskId, sessionId2, humanApprovedCritical: false));
        Assert.Equal(HttpStatusCode.Conflict, retryFail);
        Assert.Contains("approval", retryMsg ?? retryErr ?? "", StringComparison.OrdinalIgnoreCase);

        var (retryOk, _, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.DispatchRetryAsync(taskId, sessionId2, humanApprovedCritical: true));
        Assert.Equal(HttpStatusCode.OK, retryOk);
    }

    [Fact]
    public async Task Dogfood_04_stale_heartbeat_expires_with_evidence()
    {
        var (_, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);
        await LeaseTestSeed.StaleHeartbeatAsync(_fixture.Factory.Services, taskId);

        using var scope = _fixture.Factory.Services.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<LeaseRuntimeService>();
        var expired = await runtime.ProcessStaleActiveLeasesAsync();
        Assert.True(expired >= 1);

        var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await _api.GetLeaseAsync(taskId));
        Assert.Equal((int)ExecutionLeaseStatus.Blocked, OrchestrationApiClient.LeaseStatus(lease!.Value));
        var evidence = lease.Value.GetProperty("evidenceLogJson").GetString() ?? "";
        Assert.Contains("lease.expired_blocked", evidence);
        Assert.True(lease.Value.GetProperty("worktreePath").GetString()!.Length > 0);
    }

    [Fact]
    public async Task Dogfood_05_recovery_preserves_evidence_lineage()
    {
        _fixture.Factory.DietCode.FailNextDispatch = true;
        var (sessionId, taskId) = await _api.SeedTaskAsync(WorkspaceRoot());
        await _api.DispatchAsync(taskId);

        var (_, lease, _, _) = await OrchestrationApiClient.ReadAsync(await _api.GetLeaseAsync(taskId));
        var evidence = lease!.Value.GetProperty("evidenceLogJson").GetString() ?? "";
        Assert.Contains("dispatch.failed", evidence);

        var (reopen, body, _, _) = await OrchestrationApiClient.ReadAsync(
            await _api.RecoverAsync(taskId, sessionId, LeaseRecoveryMode.ReopenBlocked));
        Assert.Equal(HttpStatusCode.OK, reopen);
        var reopened = body!.Value.GetProperty("evidenceLogJson").GetString() ?? "";
        Assert.Contains("dispatch.failed", reopened);
        Assert.Contains("recovery.reopen", reopened);
        Assert.Equal((int)ExecutionLeaseStatus.Leased, OrchestrationApiClient.LeaseStatus(body.Value));
    }

    [Fact]
    public async Task Dogfood_06_agent_guardrails_reject_forbidden_commands()
    {
        var api = JzApi;
        var (_, taskId) = await api.SeedTaskAsync(WorkspaceRoot());
        await api.DispatchAsync(taskId);

        Assert.Equal(2, (await DogfoodCliRunner.RunAsync(
            JzBaseUrl, "--agent", "task", "complete", taskId.ToString(), "--yes")).ExitCode);

        Assert.Equal(2, (await DogfoodCliRunner.RunAsync(
            JzBaseUrl, "--agent", "task", "revoke", taskId.ToString(), "--yes")).ExitCode);

        Assert.Equal(2, (await DogfoodCliRunner.RunAsync(
            JzBaseUrl, "--agent", "task", "dispatch", taskId.ToString())).ExitCode);

        Assert.Equal(2, (await DogfoodCliRunner.RunAsync(
            JzBaseUrl, "--agent", "raw", "GET", "api/health")).ExitCode);
    }
}
