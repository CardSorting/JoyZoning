using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Persistence;
using JoyZoning.Persistence.Repositories;
using JoyZoning.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class KanbanExecutionOrchestratorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public KanbanExecutionOrchestratorTests()
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
        services.Configure<LeaseRuntimeOptions>(o =>
        {
            o.MaxGlobalActiveLeases = 32;
            o.Stale.LeasedMinutes = 1;
            o.Stale.RunningMinutes = 1;
            o.Duration.CriticalHours = 1;
        });
        services.AddScoped<LeaseRuntimeService>();
        services.AddSingleton<WorkspaceLiveMirrorRegistry>();
        services.Configure<WorkspaceOptions>(o => o.MirrorToSessionRoot = false);
        services.Configure<WorkspaceParallelismOptions>(_ => { });
        services.AddScoped<WorkerMergeObservabilityBuilder>();
        services.AddScoped<WorkspaceLiveMirrorObservabilityService>();
        services.AddScoped<WorkspaceLiveMirrorService>();

        var mockProxy = new Mock<IClientProxy>();
        mockProxy
            .Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var mockClients = new Mock<IHubClients>();
        mockClients.Setup(c => c.All).Returns(mockProxy.Object);
        var mockHub = new Mock<IHubContext<OperatorHub>>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        services.AddSingleton(mockHub.Object);
        services.AddSingleton<IBroccoliQBridge, DisabledBroccoliQBridge>();

        services.AddSingleton<IWorkspaceGitMerger, WorkspaceGitMerger>();
        services.AddScoped<EventIngestor>();
        services.AddScoped<KanbanExecutionOrchestrator>();

        _services = services.BuildServiceProvider();

        var db = _services.GetRequiredService<JoyZoningDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Cannot_create_second_lease_for_same_card()
    {
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        await orchestrator.BeginLeaseAsync(cardId);

        await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.BeginLeaseAsync(cardId));
    }

    [Fact]
    public async Task Critical_card_requires_human_approval_before_lease()
    {
        var (_, cardId) = await SeedCardAsync(RiskLevel.Critical);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        var ex = await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.BeginLeaseAsync(cardId, humanApprovedCritical: false));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Critical_lease_blocks_second_critical_at_leased_stage()
    {
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var (_, card1) = await SeedCardAsync(RiskLevel.Critical);
        var (_, card2) = await SeedCardAsync(RiskLevel.Critical);

        await orchestrator.BeginLeaseAsync(card1, humanApprovedCritical: true);

        var ex = await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.BeginLeaseAsync(card2, humanApprovedCritical: true));

        Assert.Contains("critical", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Critical_approval_consumed_after_running()
    {
        var (_, cardId) = await SeedCardAsync(RiskLevel.Critical);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId, humanApprovedCritical: true);
        await orchestrator.RecordDispatchAttemptAsync(cardId);
        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());

        var updated = await orchestrator.GetActiveLeaseAsync(cardId);
        Assert.True(updated!.CriticalApprovalConsumed);
    }

    [Fact]
    public async Task Dispatch_failure_moves_leased_to_blocked_with_evidence()
    {
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        await orchestrator.BeginLeaseAsync(cardId);
        var lease = await orchestrator.RecordDispatchFailureAsync(cardId, "Hermes unreachable");

        Assert.Equal(ExecutionLeaseStatus.Blocked, lease.Status);
        Assert.Contains("dispatch.failed", lease.EvidenceLogJson);
        Assert.Equal("Hermes unreachable", lease.BlockedReason);
    }

    [Fact]
    public async Task Terminal_lease_cannot_change_status()
    {
        var (_, cardId) = await SeedCardWithGitAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId);
        await WriteWorkerFileAsync(lease.WorktreePath, "done.txt", "ok");
        await orchestrator.RecordDispatchAttemptAsync(cardId);
        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());
        await orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Verifying);
        await orchestrator.SubmitVerificationAsync(cardId, PassingReport(cardId));
        await orchestrator.ApproveMergeAsync(cardId);

        await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Blocked, "late block"));
    }

    [Fact]
    public async Task Accept_result_applies_worker_files_to_canonical_workspace()
    {
        var (_, cardId) = await SeedCardWithGitAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId);
        Assert.True(Directory.Exists(lease.WorktreePath));
        await WriteWorkerFileAsync(lease.WorktreePath, "worker-change.txt", "from worker");

        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());
        await orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Verifying);
        await orchestrator.SubmitVerificationAsync(cardId, PassingReport(cardId));

        var response = await orchestrator.AcceptResultAsync(cardId);
        Assert.Equal(WorkTaskStatus.Complete, response.Task.Status);
        Assert.NotNull(response.GitConvergence);
        Assert.True(response.GitConvergence!.Succeeded, response.GitConvergence.ErrorMessage);
        Assert.NotEqual(
            response.GitConvergence.DestinationPreviousHead,
            response.GitConvergence.DestinationNewHead);

        Assert.True(File.Exists(Path.Combine(lease.WorktreePath, "worker-change.txt")));
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var history = await leases.ListByTaskIdAsync(cardId);
        Assert.Contains(history, l => l.EvidenceLogJson.Contains("git.convergence.succeeded", StringComparison.Ordinal));
        Assert.Null(await orchestrator.GetActiveLeaseAsync(cardId));
    }

    [Fact]
    public async Task Accept_result_does_not_mark_merged_when_git_convergence_fails()
    {
        var (_, cardId) = await SeedCardWithGitAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();

        var task = await tasks.GetByIdAsync(cardId);
        var session = await sessions.GetByIdAsync(task!.OperatorSessionId);
        var root = session!.WorkspaceRoot;

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId);
        await GitInitCommitOnBranchAsync(root, lease.BranchName, lease.WorktreePath, "worker-change.txt", "from worker");

        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());
        await orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Verifying);
        await orchestrator.SubmitVerificationAsync(cardId, PassingReport(cardId));

        await GitCommandRunner.RunAsync(root, "checkout main", default);
        await File.WriteAllTextAsync(Path.Combine(root, "conflict.txt"), "main edit");
        await GitCommandRunner.RunAsync(root, "add conflict.txt", default);
        await GitCommandRunner.RunAsync(root, "commit -m \"main conflict\"", default);
        await GitCommandRunner.RunAsync(root, $"checkout \"{lease.BranchName}\"", default);
        await File.WriteAllTextAsync(Path.Combine(root, "conflict.txt"), "worker edit");
        await GitCommandRunner.RunAsync(root, "add conflict.txt", default);
        await GitCommandRunner.RunAsync(root, "commit -m \"worker conflict\"", default);
        await GitCommandRunner.RunAsync(root, "checkout main", default);

        var ex = await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.AcceptResultAsync(cardId));
        Assert.Equal(409, ex.StatusCode);

        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var active = await leases.GetActiveByTaskIdAsync(cardId);
        Assert.NotNull(active);
        Assert.Equal(ExecutionLeaseStatus.ReadyForReview, active!.Status);
        Assert.True(
            active.EvidenceLogJson.Contains("git.convergence.failed", StringComparison.Ordinal)
            || active.BlockedReason?.Contains("conflict", StringComparison.OrdinalIgnoreCase) == true
            || ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("merge", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Accept_result_fails_when_worktree_missing()
    {
        var (_, cardId) = await SeedCardWithGitAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId);
        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());
        await orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Verifying);
        await orchestrator.SubmitVerificationAsync(cardId, PassingReport(cardId));

        lease.WorktreePath = string.Empty;
        await _services.GetRequiredService<IExecutionLeaseRepository>().UpdateAsync(lease);

        await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.AcceptResultAsync(cardId));

        var active = await leases.GetActiveByTaskIdAsync(cardId);
        Assert.Equal(ExecutionLeaseStatus.ReadyForReview, active!.Status);
    }

    [Fact]
    public async Task Failed_verification_stays_verifying_not_reviewable()
    {
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        await orchestrator.BeginLeaseAsync(cardId);
        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());
        await orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Verifying);

        var lease = await orchestrator.SubmitFailedVerificationAsync(cardId, new VerificationReport
        {
            CardId = cardId,
            SessionId = Guid.NewGuid(),
            CommandsRun = [new CommandRunSummary { Command = "dotnet test", Passed = false, Summary = "failed" }],
            ReadyForHumanReview = false,
        });

        Assert.Equal(ExecutionLeaseStatus.Verifying, lease.Status);
        Assert.NotNull(lease.FailedVerificationReportJson);
        Assert.Null(lease.VerificationReportJson);
    }

    [Fact]
    public async Task DietCode_cannot_complete_task_via_validator()
    {
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        await Assert.ThrowsAsync<LeaseOrchestrationException>(() =>
            orchestrator.ValidateTaskStatusChangeAsync(
                cardId, WorkTaskStatus.Complete, StatusChangeActor.DietCode));
    }

    [Fact]
    public async Task Human_merge_is_only_path_to_complete()
    {
        var (_, cardId) = await SeedCardWithGitAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        var (lease, _) = await orchestrator.BeginLeaseAsync(cardId);
        await WriteWorkerFileAsync(lease.WorktreePath, "done.txt", "ok");
        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());
        await orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Verifying);
        await orchestrator.SubmitVerificationAsync(cardId, PassingReport(cardId));

        var response = await orchestrator.ApproveMergeAsync(cardId);
        Assert.Equal(WorkTaskStatus.Complete, response.Task.Status);
        Assert.True(response.GitConvergence?.Succeeded);
    }

    [Fact]
    public async Task Revoked_lease_preserves_verification_and_worktree_refs()
    {
        var (_, cardId) = await SeedCardAsync(RiskLevel.Low);
        var orchestrator = _services.GetRequiredService<KanbanExecutionOrchestrator>();

        var (created, _) = await orchestrator.BeginLeaseAsync(cardId);
        await orchestrator.MarkLeaseRunningAsync(cardId, Guid.NewGuid());
        await orchestrator.AgentTransitionLeaseAsync(cardId, ExecutionLeaseStatus.Verifying);
        await orchestrator.SubmitVerificationAsync(cardId, PassingReport(cardId));

        var revoked = await orchestrator.RevokeLeaseAsync(cardId, "operator stop");
        Assert.Equal(ExecutionLeaseStatus.Revoked, revoked.Status);
        Assert.NotNull(revoked.VerificationReportJson);
        Assert.Contains(created.WorktreePath, revoked.EvidenceLogJson);
    }

    private static VerificationReport PassingReport(Guid cardId) => new()
    {
        CardId = cardId,
        SessionId = Guid.NewGuid(),
        CommandsRun = [new CommandRunSummary { Command = "dotnet build", Passed = true, Summary = "ok" }],
        ReadyForHumanReview = true,
    };

    private Task<(Guid SessionId, Guid CardId)> SeedCardWithGitAsync(RiskLevel risk) =>
        SeedCardAsync(risk, initGit: true);

    private static async Task WriteWorkerFileAsync(string worktreePath, string fileName, string content)
    {
        Directory.CreateDirectory(worktreePath);
        await File.WriteAllTextAsync(Path.Combine(worktreePath, fileName), content);
    }

    private static async Task GitInitCommitOnBranchAsync(
        string root,
        string branchName,
        string worktreePath,
        string fileName,
        string content)
    {
        await GitCommandRunner.RunAsync(root, $"checkout -b \"{branchName}\"", default);
        Directory.CreateDirectory(worktreePath);
        await File.WriteAllTextAsync(Path.Combine(worktreePath, fileName), content);
        var rel = Path.Combine(Path.GetRelativePath(root, worktreePath), fileName).Replace('\\', '/');
        await GitCommandRunner.RunAsync(root, $"add \"{rel}\"", default);
        await GitCommandRunner.RunAsync(root, "commit -m \"worker work\"", default);
        await GitCommandRunner.RunAsync(root, "checkout main", default);
    }

    private async Task<(Guid SessionId, Guid CardId)> SeedCardAsync(RiskLevel risk, bool initGit = false)
    {
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var now = DateTimeOffset.UtcNow;
        var workspace = Path.Combine(Path.GetTempPath(), "jz-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        if (initGit)
        {
            await GitCommandRunner.RunAsync(workspace, "init", default);
            await GitCommandRunner.RunAsync(workspace, "config user.email \"test@joyzoning.local\"", default);
            await GitCommandRunner.RunAsync(workspace, "config user.name \"JoyZoning Test\"", default);
            await File.WriteAllTextAsync(Path.Combine(workspace, "README.md"), "main");
            await GitCommandRunner.RunAsync(workspace, "add README.md", default);
            await GitCommandRunner.RunAsync(workspace, "commit -m \"init\"", default);
            await GitCommandRunner.RunAsync(workspace, "branch -M main", default);
        }

        var session = new OperatorSession
        {
            Id = Guid.NewGuid(),
            Name = "test",
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
            Description = "Objective line",
            Risk = risk,
            Status = WorkTaskStatus.Planned,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await tasks.CreateAsync(task);
        return (session.Id, task.Id);
    }
}
