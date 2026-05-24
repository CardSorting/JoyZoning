using JoyZoning.Agents;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.ControlPlane.Background;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Tests.Infrastructure;
using JoyZoning.Persistence;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class LeaseRuntimeServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public LeaseRuntimeServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddUnitTestHost();
        services.AddDbContext<JoyZoningDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
        services.AddScoped<IOperatorSessionRepository, OperatorSessionRepository>();
        services.AddScoped<IExecutionLeaseRepository, ExecutionLeaseRepository>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();
        services.AddScoped<IEventRepository, EventRepository>();

        var mockHub = new Mock<IHubContext<OperatorHub>>();
        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(c => c.All).Returns(Mock.Of<IClientProxy>());
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        services.AddSingleton(mockHub.Object);
        services.AddSingleton<IBroccoliQBridge, DisabledBroccoliQBridge>();

        services.AddScoped<EventIngestor>();
        services.Configure<LeaseRuntimeOptions>(o =>
        {
            o.MaxGlobalActiveLeases = 2;
            o.MaxActiveLeasesPerSession = 2;
            o.Stale.LeasedMinutes = 30;
            o.Stale.RunningMinutes = 30;
            o.Stale.CriticalLeasedMinutes = 30;
            o.Stale.CriticalRunningMinutes = 30;
            o.Duration.LowHours = 8;
            o.Duration.CriticalHours = 8;
        });
        services.AddSingleton<TestDietCodeAdapter>();
        services.AddSingleton<TestHermesAdapter>();
        services.AddSingleton<IAgentAdapter>(sp => sp.GetRequiredService<TestDietCodeAdapter>());
        services.AddSingleton<IAgentAdapter>(sp => sp.GetRequiredService<TestHermesAdapter>());
        services.AddSingleton<AgentAdapterRegistry>(sp =>
            new AgentAdapterRegistry(sp.GetServices<IAgentAdapter>()));
        services.AddSingleton<HermesRunEventConsumer>();
        services.Configure<ExecutorOptions>(_ => { });
        services.AddLogging();
        services.AddScoped<LeaseRuntimeService>();
        services.Configure<WorkspaceOptions>(_ => { });
        services.Configure<WorkspaceParallelismOptions>(_ => { });
        services.AddScoped<WorkerMergeObservabilityBuilder>();
        services.AddScoped<WorkspaceWorkerObservabilityService>();
        services.Configure<AuthorityOptions>(o =>
        {
            o.AutopilotEnabled = true;
            o.DefaultProfile = JoyZoning.Domain.Configuration.AuthorityProfileKind.BalancedAuto;
        });
        services.AddSingleton<JoyZoning.Domain.Orchestration.IWorkspaceGitMerger, JoyZoning.Adapters.Workspace.WorkspaceGitMerger>();
        services.AddScoped<AuthorityAutopilotMergeContextBuilder>();
        services.AddScoped<AuthorityAutopilotService>();
        services.AddScoped<KanbanExecutionOrchestrator>();

        _services = services.BuildServiceProvider();
        _services.GetRequiredService<JoyZoningDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Stale_leased_lease_detected_when_heartbeat_old()
    {
        var options = new LeaseRuntimeOptions { Stale = { LeasedMinutes = 5 } };
        var lease = new ExecutionLease
        {
            Status = ExecutionLeaseStatus.Leased,
            RiskLevel = LeaseRiskLevel.Low,
            LastHeartbeatAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
        };

        Assert.True(LeaseSchedulerPolicy.IsHeartbeatStale(lease, DateTimeOffset.UtcNow, options));
    }

    [Fact]
    public async Task Expire_stale_lease_blocks_with_evidence()
    {
        var runtime = _services.GetRequiredService<LeaseRuntimeService>();
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId);
        lease.LastHeartbeatAt = DateTimeOffset.UtcNow.AddHours(-2);
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        await leases.UpdateAsync(lease);

        var expired = await runtime.ExpireIfStaleAsync(lease, DateTimeOffset.UtcNow);
        Assert.NotNull(expired);
        Assert.Equal(ExecutionLeaseStatus.Blocked, expired!.Status);
        Assert.Contains("lease.expired_blocked", expired.EvidenceLogJson);
        Assert.False(expired.CriticalApprovalGranted);
    }

    [Fact]
    public async Task Heartbeat_refresh_updates_timestamp()
    {
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        await orchestrator.BeginLeaseAsync(cardId);

        var sessionId = (await _services.GetRequiredService<IWorkTaskRepository>().GetByIdAsync(cardId))!
            .OperatorSessionId;
        var before = DateTimeOffset.UtcNow.AddMinutes(-5);
        var leaseRepo = _services.GetRequiredService<IExecutionLeaseRepository>();
        var lease = await leaseRepo.GetActiveByTaskIdAsync(cardId);
        lease!.LastHeartbeatAt = before;
        await leaseRepo.UpdateAsync(lease);

        var updated = await orchestrator.RecordHeartbeatAsync(
            cardId, new LeaseOperationContext(sessionId, StatusChangeActor.DietCode));

        Assert.True(updated.LastHeartbeatAt > before);
    }

    [Fact]
    public async Task Scheduler_rejects_global_overload()
    {
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, card1) = await SeedCardAsync(RiskLevel.Low);
        var (_, card2) = await SeedCardAsync(RiskLevel.Low);
        var (_, card3) = await SeedCardAsync(RiskLevel.Low);

        await orchestrator.BeginLeaseAsync(card1);
        await orchestrator.BeginLeaseAsync(card2);

        await Assert.ThrowsAsync<LeaseOrchestrationException>(() => orchestrator.BeginLeaseAsync(card3));
    }

    [Fact]
    public async Task Recovery_reopen_blocked_preserves_evidence_chain()
    {
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        await orchestrator.BeginLeaseAsync(cardId);
        await orchestrator.RecordDispatchFailureAsync(cardId, "dispatch fail");

        var sessionId = (await _services.GetRequiredService<IWorkTaskRepository>().GetByIdAsync(cardId))!
            .OperatorSessionId;
        var recovered = await orchestrator.RecoverLeaseAsync(
            cardId,
            LeaseRecoveryMode.ReopenBlocked,
            new LeaseOperationContext(sessionId, StatusChangeActor.Human, RecoveryFlow: true));

        Assert.Equal(ExecutionLeaseStatus.Leased, recovered.Status);
        Assert.Contains("dispatch.failed", recovered.EvidenceLogJson);
        Assert.Contains("recovery.reopen", recovered.EvidenceLogJson);
    }

    [Fact]
    public async Task Dispatch_retry_appends_evidence_and_requires_new_critical_approval()
    {
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Critical);
        var sessionId = (await _services.GetRequiredService<IWorkTaskRepository>().GetByIdAsync(cardId))!
            .OperatorSessionId;

        await orchestrator.BeginLeaseAsync(cardId, humanApprovedCritical: true);
        await orchestrator.RecordDispatchAttemptAsync(cardId);
        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());

        await orchestrator.AgentTransitionLeaseAsync(
            cardId, ExecutionLeaseStatus.Blocked, "stop", new LeaseOperationContext(sessionId, StatusChangeActor.DietCode));
        await orchestrator.RecoverLeaseAsync(
            cardId, LeaseRecoveryMode.ReopenBlocked,
            new LeaseOperationContext(sessionId, StatusChangeActor.Human, RecoveryFlow: true),
            humanApprovedCritical: true);

        var lease = await orchestrator.GetActiveLeaseAsync(cardId);
        Assert.False(lease!.CriticalApprovalConsumed);

        await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.RecordDispatchRetryAsync(
                cardId, humanApprovedCritical: false,
                new LeaseOperationContext(sessionId, StatusChangeActor.Human)));

        await orchestrator.RecordDispatchRetryAsync(
            cardId, humanApprovedCritical: true,
            new LeaseOperationContext(sessionId, StatusChangeActor.Human));

        lease = await orchestrator.GetActiveLeaseAsync(cardId);
        Assert.Contains("dispatch.retry", lease!.EvidenceLogJson);
        Assert.True(lease.DispatchAttemptCount >= 2);
    }

    [Fact]
    public async Task Critical_approval_cleared_after_expiration()
    {
        var runtime = _services.GetRequiredService<LeaseRuntimeService>();
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Critical);

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId, humanApprovedCritical: true);
        lease.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        lease.LastHeartbeatAt = DateTimeOffset.UtcNow.AddHours(-1);
        await _services.GetRequiredService<IExecutionLeaseRepository>().UpdateAsync(lease);

        var expired = await runtime.ExpireIfStaleAsync(lease, DateTimeOffset.UtcNow);
        Assert.NotNull(expired);
        Assert.False(expired!.CriticalApprovalGranted);
        Assert.False(expired.CriticalApprovalConsumed);
    }

    [Fact]
    public async Task Wrong_session_cannot_heartbeat()
    {
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        await orchestrator.BeginLeaseAsync(cardId);

        await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.RecordHeartbeatAsync(
                cardId,
                new LeaseOperationContext(Guid.NewGuid(), StatusChangeActor.DietCode)));
    }

    [Fact]
    public async Task Reconciliation_repairs_orphaned_running_lease()
    {
        var runtime = _services.GetRequiredService<LeaseRuntimeService>();
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var executions = _services.GetRequiredService<IExecutionRepository>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);

        await orchestrator.BeginLeaseAsync(cardId);
        await orchestrator.RecordDispatchAttemptAsync(cardId);
        var execId = Guid.NewGuid();
        await orchestrator.MarkLeaseRunningAsync(cardId, execId);

        await executions.CreateAsync(new ExecutionSession
        {
            Id = execId,
            WorkTaskId = cardId,
            HermesRunId = "run-1",
            Objective = "test",
            Phase = ExecutionPhase.Completed,
            StartedAt = DateTimeOffset.UtcNow,
        });

        var report = await runtime.ReconcileAsync();
        Assert.Equal(1, report.OrphanedRunning);

        var lease = await orchestrator.GetActiveLeaseAsync(cardId);
        Assert.Equal(ExecutionLeaseStatus.Verifying, lease!.Status);
        Assert.Contains("execution.completed", lease.EvidenceLogJson);
    }

    [Fact]
    public async Task Reconciliation_does_not_metadata_merge_ready_lease_without_git_evidence()
    {
        var runtime = _services.GetRequiredService<LeaseRuntimeService>();
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        await orchestrator.BeginLeaseAsync(cardId);

        var task = await tasks.GetByIdAsync(cardId);
        await tasks.UpdateStatusAsync(cardId, WorkTaskStatus.Complete);

        var lease = await leases.GetActiveByTaskIdAsync(cardId);
        Assert.NotNull(lease);
        lease!.Status = ExecutionLeaseStatus.ReadyForReview;
        lease.VerificationReportJson = VerificationReportSerializer.Serialize(new VerificationReport
        {
            CardId = cardId,
            SessionId = task!.OperatorSessionId,
            ChangedFiles = ["docs/a.md"],
            CommandsRun = [new CommandRunSummary { Command = "true", Passed = true, Summary = "ok" }],
            ReadyForHumanReview = true,
        });
        await leases.UpdateAsync(lease);

        var report = await runtime.ReconcileAsync();
        Assert.Equal(1, report.MergedTaskActiveLease);

        var updated = await leases.GetActiveByTaskIdAsync(cardId);
        Assert.NotNull(updated);
        Assert.Equal(ExecutionLeaseStatus.Blocked, updated!.Status);
        var evidence = LeaseEvidenceLog.DeserializeEntries(updated.EvidenceLogJson);
        Assert.DoesNotContain(evidence, e => e.Kind == GitConvergenceEvidence.SucceededKind);
        Assert.Contains("without git.convergence.succeeded", updated.BlockedReason ?? "", StringComparison.Ordinal);
        Assert.Contains(evidence, e => e.Kind == "reconciliation.repair");
    }

    [Fact]
    public async Task Reconciliation_keeps_running_when_hermes_poll_still_active()
    {
        var dietCode = _services.GetRequiredService<TestDietCodeAdapter>();
        dietCode.PollOverride = runId => new AgentRunPollResult(runId, "running", false);

        var runtime = _services.GetRequiredService<LeaseRuntimeService>();
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var executions = _services.GetRequiredService<IExecutionRepository>();
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);

        await orchestrator.BeginLeaseAsync(cardId);
        await orchestrator.RecordDispatchAttemptAsync(cardId);
        var execId = Guid.NewGuid();
        await orchestrator.MarkLeaseRunningAsync(cardId, execId);

        await executions.CreateAsync(new ExecutionSession
        {
            Id = execId,
            WorkTaskId = cardId,
            HermesRunId = "run-active",
            Objective = "test",
            Phase = ExecutionPhase.Interrupted,
            StartedAt = DateTimeOffset.UtcNow,
        });

        var report = await runtime.ReconcileAsync();
        Assert.Equal(1, report.OrphanedRunning);

        var lease = await orchestrator.GetActiveLeaseAsync(cardId);
        Assert.Equal(ExecutionLeaseStatus.Running, lease!.Status);
        Assert.Null(lease.BlockedReason);
    }

    private async Task<(Guid SessionId, Guid CardId)> SeedCardAsync(RiskLevel risk)
    {
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var now = DateTimeOffset.UtcNow;
        var workspace = Path.Combine(Path.GetTempPath(), "jz-rt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        var session = new OperatorSession
        {
            Id = Guid.NewGuid(),
            Name = "rt",
            WorkspaceRoot = workspace,
            Status = SessionStatus.Idle,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await sessions.CreateAsync(session);

        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            OperatorSessionId = session.Id,
            Title = "Card",
            Description = "Objective",
            Risk = risk,
            Status = WorkTaskStatus.Planned,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await tasks.CreateAsync(task);
        return (session.Id, task.Id);
    }
}
