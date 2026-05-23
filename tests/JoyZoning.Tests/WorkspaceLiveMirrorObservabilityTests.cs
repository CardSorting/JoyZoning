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
using System.Text.Json;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorkspaceLiveMirrorObservabilityTests : IDisposable
{
    private readonly string _sessionRoot;
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public WorkspaceLiveMirrorObservabilityTests()
    {
        _sessionRoot = Path.Combine(Path.GetTempPath(), "jz-obs-" + Guid.NewGuid().ToString("N"));
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
        services.Configure<WorkspaceOptions>(o => o.MirrorToSessionRoot = true);
        services.Configure<WorkspaceParallelismOptions>(o =>
        {
            o.LiveMirrorMode = LiveMirrorMode.PerExecution;
            o.DisableSharedSessionRootMirrorWhenParallel = true;
        });
        services.AddSingleton<WorkspaceLiveMirrorRegistry>();
        services.AddScoped<WorkerMergeObservabilityBuilder>();
        services.AddScoped<WorkspaceLiveMirrorObservabilityService>();
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
    public async Task Index_json_lists_multiple_active_mirrors()
    {
        var sessionId = await SeedSessionAsync();
        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        var observability = _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>();

        var (_, leaseA, worktreeA) = await SeedLeaseAsync(sessionId, "alpha");
        var (_, leaseB, worktreeB) = await SeedLeaseAsync(sessionId, "beta");
        await File.WriteAllTextAsync(Path.Combine(worktreeA, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(worktreeB, "b.txt"), "b");

        Assert.NotNull(await mirror.RefreshForLeaseAsync(leaseA));
        Assert.NotNull(await mirror.RefreshForLeaseAsync(leaseB));

        var session = await _services.GetRequiredService<IOperatorSessionRepository>().GetByIdAsync(sessionId);
        Assert.NotNull(session);
        await observability.WriteIndexFileAsync(session!, activeLeasesInWorkspace: 2);

        var indexPath = Path.Combine(_sessionRoot, ".joyzoning", "live", "index.json");
        Assert.True(File.Exists(indexPath));
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(indexPath));
        Assert.True(doc.RootElement.GetProperty("parallelActive").GetBoolean());
        Assert.Equal(2, doc.RootElement.GetProperty("mirrors").GetArrayLength());
        Assert.False(doc.RootElement.GetProperty("sessionRootIsCanonicalLiveState").GetBoolean());
    }

    [Fact]
    public async Task Stale_and_completed_lifecycle_surfaces_in_read_model()
    {
        var sessionId = await SeedSessionAsync();
        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        var observability = _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>();
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();

        var (_, lease, worktree) = await SeedLeaseAsync(sessionId, "lifecycle");
        await File.WriteAllTextAsync(Path.Combine(worktree, "x.txt"), "x");
        await mirror.RefreshForLeaseAsync(lease);

        await mirror.MarkMirrorStaleForExecutionEndAsync(lease);
        var staleModel = await observability.GetParallelWorkersAsync(sessionId);
        var staleWorker = staleModel.Workers.Single();
        Assert.Equal("stale", staleWorker.LifecycleStatus);
        Assert.Equal("stale", staleWorker.HealthState);

        await mirror.CompleteMirrorForLeaseAsync(lease);
        var doneModel = await observability.GetParallelWorkersAsync(sessionId);
        var doneWorker = doneModel.Workers.Single(w => w.TaskTitle == "lifecycle");
        Assert.Equal("completed", doneWorker.LifecycleStatus);
    }

    [Fact]
    public async Task Collision_skip_is_surfaced()
    {
        var registry = _services.GetRequiredService<WorkspaceLiveMirrorRegistry>();
        var observability = _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>();
        var sessionId = await SeedSessionAsync();
        var (_, leaseA, _) = await SeedLeaseAsync(sessionId, "collision-a");
        var (_, leaseB, _) = await SeedLeaseAsync(sessionId, "collision-b");

        var path = Path.Combine(_sessionRoot, ".joyzoning", "live", leaseA.WorkTaskId.ToString("D"), leaseA.Id.ToString("D"));
        registry.TryAcquire(path, leaseA.Id, leaseA.WorkTaskId, leaseA.Id);
        registry.RecordOutcome(
            leaseB.Id,
            LiveMirrorHealthState.SkippedCollision,
            "Mirror path held by lease A.",
            path,
            leaseA.Id);

        var model = await observability.GetParallelWorkersAsync(sessionId);
        Assert.Contains(model.Workers, w => w.HealthState == "skipped_collision");
        Assert.Contains(model.Warnings, w => w.Code == "skipped_collision");
    }

    [Fact]
    public async Task Parallel_execution_session_root_is_not_canonical()
    {
        var sessionId = await SeedSessionAsync();
        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        var observability = _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>();

        var (_, leaseA, worktreeA) = await SeedLeaseAsync(sessionId, "p1");
        var (_, leaseB, worktreeB) = await SeedLeaseAsync(sessionId, "p2");
        await File.WriteAllTextAsync(Path.Combine(worktreeA, "1.txt"), "1");
        await File.WriteAllTextAsync(Path.Combine(worktreeB, "2.txt"), "2");
        await mirror.RefreshForLeaseAsync(leaseA);
        await mirror.RefreshForLeaseAsync(leaseB);

        var model = await observability.GetParallelWorkersAsync(sessionId);
        Assert.True(model.ParallelActive);
        Assert.False(model.SessionRootIsCanonicalLiveState);
        Assert.Contains("not canonical", model.CanonicalLiveStateHint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, model.Workers.Count);
        Assert.All(model.Workers, w => Assert.False(w.IsSharedSessionRootMirror));
    }

    private async Task<Guid> SeedSessionAsync()
    {
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var id = Guid.NewGuid();
        await sessions.CreateAsync(new OperatorSession
        {
            Id = id,
            Name = "obs",
            WorkspaceRoot = _sessionRoot,
            WorkspaceKey = WorkspacePaths.Normalize(_sessionRoot),
            Status = SessionStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        return id;
    }

    private async Task<(Guid TaskId, ExecutionLease Lease, string Worktree)> SeedLeaseAsync(
        Guid sessionId,
        string title)
    {
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var taskId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var worktree = Path.Combine(_sessionRoot, ".joyzoning", "worktrees", taskId.ToString("N"));
        Directory.CreateDirectory(worktree);

        await tasks.CreateAsync(new WorkTask
        {
            Id = taskId,
            OperatorSessionId = sessionId,
            Title = title,
            Status = WorkTaskStatus.InProgress,
            KanbanRevision = 2,
            KanbanPushedRevision = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = taskId,
            OperatorSessionId = sessionId,
            AssignedSessionId = sessionId,
            ExecutionSessionId = executionId,
            WorktreePath = worktree,
            BranchName = "jz/test",
            Status = ExecutionLeaseStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
        };
        await leases.CreateAsync(lease);
        return (taskId, lease, worktree);
    }
}
