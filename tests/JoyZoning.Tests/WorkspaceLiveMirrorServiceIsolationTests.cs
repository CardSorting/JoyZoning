using JoyZoning.ControlPlane.Services;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence;
using JoyZoning.Persistence.Repositories;
using JoyZoning.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceLiveMirrorServiceIsolationTests : IDisposable
{
    private readonly string _sessionRoot;
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public WorkspaceLiveMirrorServiceIsolationTests()
    {
        _sessionRoot = Path.Combine(Path.GetTempPath(), "jz-live-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sessionRoot);
        Directory.CreateDirectory(Path.Combine(_sessionRoot, ".joyzoning", "worktrees"));

        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddUnitTestHost();
        services.AddDbContext<JoyZoningDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<IOperatorSessionRepository, OperatorSessionRepository>();
        services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
        services.AddScoped<IExecutionLeaseRepository, ExecutionLeaseRepository>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();
        services.AddSingleton<WorkspaceLiveMirrorRegistry>();
        services.AddScoped<WorkerMergeObservabilityBuilder>();
        services.AddScoped<AuthorityAutopilotMergeContextBuilder>();
        services.AddScoped<AuthorityAutopilotService>();
        services.Configure<AuthorityOptions>(_ => { });
        services.Configure<LeaseRuntimeOptions>(_ => { });
        services.AddScoped<WorkspaceLiveMirrorObservabilityService>();

        services.Configure<WorkspaceOptions>(o =>
        {
            o.MirrorToSessionRoot = true;
            o.WriteLiveJsonFile = true;
        });
        services.Configure<WorkspaceParallelismOptions>(o =>
        {
            o.LiveMirrorMode = LiveMirrorMode.PerExecution;
            o.DisableSharedSessionRootMirrorWhenParallel = true;
        });
        services.AddSingleton<WorkspaceLiveMirrorRegistry>();
        services.AddScoped<WorkspaceLiveMirrorService>();

        _services = services.BuildServiceProvider();
        _services.GetRequiredService<JoyZoningDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
        try { Directory.Delete(_sessionRoot, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public async Task Two_active_leases_mirror_to_different_live_paths()
    {
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();

        var sessionId = Guid.NewGuid();
        await sessions.CreateAsync(new OperatorSession
        {
            Id = sessionId,
            Name = "test",
            WorkspaceRoot = _sessionRoot,
            WorkspaceKey = WorkspacePaths.Normalize(_sessionRoot),
            Status = SessionStatus.Executing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var (_, leaseA, worktreeA) = await SeedLeaseAsync(sessions, tasks, leases, sessionId, "task-a");
        var (_, leaseB, worktreeB) = await SeedLeaseAsync(sessions, tasks, leases, sessionId, "task-b");

        await File.WriteAllTextAsync(Path.Combine(worktreeA, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(worktreeB, "b.txt"), "b");

        var snapA = await mirror.RefreshForLeaseAsync(leaseA);
        var snapB = await mirror.RefreshForLeaseAsync(leaseB);

        Assert.NotNull(snapA);
        Assert.NotNull(snapB);
        Assert.NotEqual(snapA!.LiveMirrorRoot, snapB!.LiveMirrorRoot);
        Assert.False(snapA.IsSharedSessionRootMirror);
        Assert.False(snapB.IsSharedSessionRootMirror);
        Assert.True(File.Exists(Path.Combine(snapA.LiveMirrorRoot, "a.txt")));
        Assert.True(File.Exists(Path.Combine(snapB.LiveMirrorRoot, "b.txt")));
        Assert.False(File.Exists(Path.Combine(snapA.LiveMirrorRoot, "b.txt")));
        Assert.False(File.Exists(Path.Combine(_sessionRoot, "a.txt")));
    }

    [Fact]
    public async Task Shared_session_root_used_when_single_active_lease_and_legacy_mode()
    {
        var options = Options.Create(new WorkspaceParallelismOptions
        {
            LiveMirrorMode = LiveMirrorMode.SharedSessionRoot,
            DisableSharedSessionRootMirrorWhenParallel = true,
        });
        var workspace = Options.Create(new WorkspaceOptions { MirrorToSessionRoot = true });
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();

        var registry = new WorkspaceLiveMirrorRegistry();
        var authorityOptions = Options.Create(new AuthorityOptions());
        var observability = new WorkspaceLiveMirrorObservabilityService(
            sessions,
            tasks,
            leases,
            _services.GetRequiredService<IExecutionRepository>(),
            workspace,
            options,
            authorityOptions,
            registry,
            new WorkerMergeObservabilityBuilder(),
            _services.GetRequiredService<AuthorityAutopilotService>());
        var mirror = new WorkspaceLiveMirrorService(
            sessions,
            tasks,
            leases,
            _services.GetRequiredService<IExecutionRepository>(),
            workspace,
            options,
            registry,
            observability,
            NullLogger<WorkspaceLiveMirrorService>.Instance);

        var sessionId = Guid.NewGuid();
        await sessions.CreateAsync(new OperatorSession
        {
            Id = sessionId,
            Name = "test",
            WorkspaceRoot = _sessionRoot,
            WorkspaceKey = WorkspacePaths.Normalize(_sessionRoot),
            Status = SessionStatus.Executing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var (_, lease, worktree) = await SeedLeaseAsync(sessions, tasks, leases, sessionId, "solo");
        await File.WriteAllTextAsync(Path.Combine(worktree, "solo.txt"), "solo");

        var snap = await mirror.RefreshForLeaseAsync(lease);
        Assert.NotNull(snap);
        Assert.True(snap!.IsSharedSessionRootMirror);
        Assert.Equal(Path.GetFullPath(_sessionRoot), Path.GetFullPath(snap.LiveMirrorRoot));
        Assert.True(File.Exists(Path.Combine(_sessionRoot, "solo.txt")));
    }

    private async Task<(Guid TaskId, ExecutionLease Lease, string Worktree)> SeedLeaseAsync(
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IExecutionLeaseRepository leases,
        Guid sessionId,
        string title)
    {
        var taskId = Guid.NewGuid();
        var worktree = Path.Combine(_sessionRoot, ".joyzoning", "worktrees", taskId.ToString("N"));
        Directory.CreateDirectory(worktree);

        await tasks.CreateAsync(new WorkTask
        {
            Id = taskId,
            OperatorSessionId = sessionId,
            Title = title,
            Status = WorkTaskStatus.InProgress,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var executionId = Guid.NewGuid();
        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = taskId,
            OperatorSessionId = sessionId,
            AssignedSessionId = sessionId,
            ExecutionSessionId = executionId,
            WorktreePath = worktree,
            BranchName = "jz/" + taskId.ToString("N")[..8],
            Status = ExecutionLeaseStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
        };
        await leases.CreateAsync(lease);
        return (taskId, lease, worktree);
    }
}
