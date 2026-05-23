using System.Text.Json;
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
public class WorkerMergeObservabilityTests : IDisposable
{
    private readonly string _sessionRoot;
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public WorkerMergeObservabilityTests()
    {
        _sessionRoot = Path.Combine(Path.GetTempPath(), "jz-merge-" + Guid.NewGuid().ToString("N"));
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
            o.LiveMirrorRetentionDays = 1;
        });
        services.AddSingleton<WorkspaceLiveMirrorRegistry>();
        services.AddScoped<WorkerMergeObservabilityBuilder>();
        services.AddScoped<WorkspaceLiveMirrorObservabilityService>();
        services.AddScoped<WorkspaceLiveMirrorService>();
        services.AddLogging();

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
    public async Task Decision_summary_and_guardrails_populated_for_ready_worker()
    {
        var sessionId = await SeedSessionAsync();
        var (_, lease, worktree) = await SeedLeaseAsync(sessionId, "decision", ExecutionLeaseStatus.ReadyForReview);
        await AttachPassingVerificationAsync(lease, ["src/decision.cs"]);
        await File.WriteAllTextAsync(Path.Combine(worktree, "decision.cs"), "x");

        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        await mirror.RefreshForLeaseAsync(lease);

        var worker = (await _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>()
            .GetParallelWorkersAsync(sessionId)).Workers.Single();

        Assert.Equal("ready_to_merge", worker.MergeState);
        Assert.False(worker.ApproveGuardrails.Blocked);
        Assert.Equal("passed", worker.DecisionSummary.VerificationStatus);
        Assert.Equal(worktree, worker.DecisionSummary.WorktreePath);
        Assert.NotNull(worker.DecisionSummary.LiveMirrorPath);
        Assert.Equal(OperationalModeNavigation.SlugReview, worker.RecommendedModeSlug);
        Assert.NotEmpty(worker.AvailableModeTransitions);
    }

    [Fact]
    public async Task Completed_lease_with_passing_verification_appears_ready_to_merge()
    {
        var sessionId = await SeedSessionAsync();
        var (taskId, lease, worktree) = await SeedLeaseAsync(sessionId, "ready", ExecutionLeaseStatus.ReadyForReview);
        await AttachPassingVerificationAsync(lease, ["src/a.cs", "src/b.cs"]);
        await File.WriteAllTextAsync(Path.Combine(worktree, "a.cs"), "a");

        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        await mirror.RefreshForLeaseAsync(lease);

        var model = await _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>()
            .GetParallelWorkersAsync(sessionId);
        var worker = model.Workers.Single(w => w.LeaseId == lease.Id);

        Assert.Equal("ready_to_merge", worker.MergeState);
        Assert.NotNull(worker.MergeReadiness);
        Assert.Equal(2, worker.MergeReadiness!.ChangedFilesCount);
        Assert.Contains("src/a.cs", worker.MergeReadiness.ChangedFilesSummary);
        Assert.True(worker.MergeReadiness.VerificationPassed);
    }

    [Fact]
    public async Task Merged_lease_shows_merged_not_ready_to_merge()
    {
        var sessionId = await SeedSessionAsync();
        var (_, lease, worktree) = await SeedLeaseAsync(sessionId, "merged", ExecutionLeaseStatus.ReadyForReview);
        await AttachPassingVerificationAsync(lease);
        await File.WriteAllTextAsync(Path.Combine(worktree, "done.txt"), "x");

        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        await mirror.RefreshForLeaseAsync(lease);

        lease.Status = ExecutionLeaseStatus.Merged;
        lease.MergedAt = DateTimeOffset.UtcNow;
        await _services.GetRequiredService<IExecutionLeaseRepository>().UpdateAsync(lease);
        await mirror.CompleteMirrorForLeaseAsync(lease, WorkerMergeState.Merged);

        var model = await _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>()
            .GetParallelWorkersAsync(sessionId);
        var worker = model.Workers.Single(w => w.TaskTitle == "merged");

        Assert.Equal("merged", worker.MergeState);
        Assert.Equal("Merged", worker.LeaseStatus);
        Assert.NotEqual("ready_to_merge", worker.MergeState);
    }

    [Fact]
    public async Task Revoked_lease_shows_revoked()
    {
        var sessionId = await SeedSessionAsync();
        var (_, lease, worktree) = await SeedLeaseAsync(sessionId, "revoked", ExecutionLeaseStatus.Running);
        await File.WriteAllTextAsync(Path.Combine(worktree, "x.txt"), "x");

        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        await mirror.RefreshForLeaseAsync(lease);

        lease.Status = ExecutionLeaseStatus.Revoked;
        lease.RevokedAt = DateTimeOffset.UtcNow;
        await _services.GetRequiredService<IExecutionLeaseRepository>().UpdateAsync(lease);
        await mirror.CompleteMirrorForLeaseAsync(lease, WorkerMergeState.Revoked);

        var model = await _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>()
            .GetMergeQueueAsync(sessionId);

        Assert.Single(model.RevokedAbandoned);
        Assert.Equal("revoked", model.RevokedAbandoned[0].MergeState);
        Assert.NotNull(model.RevokedAbandoned[0].LiveMirrorPath);
    }

    [Fact]
    public async Task Overlapping_ready_workers_surface_merge_conflict_with_files()
    {
        var sessionId = await SeedSessionAsync();
        var (_, leaseA, _) = await SeedLeaseAsync(sessionId, "overlap-a", ExecutionLeaseStatus.ReadyForReview);
        var (_, leaseB, _) = await SeedLeaseAsync(sessionId, "overlap-b", ExecutionLeaseStatus.ReadyForReview);
        await AttachPassingVerificationAsync(leaseA, ["src/shared.cs"]);
        await AttachPassingVerificationAsync(leaseB, ["src/shared.cs", "src/other.cs"]);

        var model = await _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>()
            .GetMergeQueueAsync(sessionId);

        Assert.Equal(2, model.MergeConflicts.Count);
        Assert.All(model.MergeConflicts, w =>
        {
            Assert.Equal("merge_conflict", w.MergeState);
            Assert.NotNull(w.MergeConflict);
            Assert.Equal("overlapping_files", w.MergeConflict!.Category);
            Assert.Contains("src/shared.cs", w.MergeConflict.ConflictFiles);
        });
        Assert.Empty(model.ReadyToMerge);
    }

    [Fact]
    public async Task Merge_queue_buckets_completed_separately_from_ready()
    {
        var sessionId = await SeedSessionAsync();
        var (_, readyLease, _) = await SeedLeaseAsync(sessionId, "q-ready", ExecutionLeaseStatus.ReadyForReview);
        var (_, mergedLease, _) = await SeedLeaseAsync(sessionId, "q-merged", ExecutionLeaseStatus.Merged);
        await AttachPassingVerificationAsync(readyLease, ["only-ready.cs"]);

        var queue = await _services.GetRequiredService<WorkspaceLiveMirrorObservabilityService>()
            .GetMergeQueueAsync(sessionId);

        Assert.Single(queue.ReadyToMerge);
        Assert.Equal("ready_to_merge", queue.ReadyToMerge[0].MergeState);
        Assert.Single(queue.CompletedWorkers);
        Assert.Equal("merged", queue.CompletedWorkers[0].MergeState);
    }

    [Fact]
    public async Task Conflicted_completed_mirror_is_not_pruned()
    {
        var sessionId = await SeedSessionAsync();
        var (_, lease, worktree) = await SeedLeaseAsync(sessionId, "conflict-prune", ExecutionLeaseStatus.ReadyForReview);
        await File.WriteAllTextAsync(Path.Combine(worktree, "z.txt"), "z");

        var mirror = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        var snapshot = await mirror.RefreshForLeaseAsync(lease);
        Assert.NotNull(snapshot);

        var metaPath = Path.Combine(snapshot!.WorktreePath, ".joyzoning", "mirror-meta.json");
        var meta = JsonSerializer.Serialize(new
        {
            status = "completed",
            mergeState = "merge_conflict",
            completedAt = DateTimeOffset.UtcNow.AddDays(-10).ToString("O"),
            leaseId = lease.Id,
            taskId = lease.WorkTaskId,
        });
        Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
        await File.WriteAllTextAsync(metaPath, meta);

        var session = await _services.GetRequiredService<IOperatorSessionRepository>().GetByIdAsync(sessionId);
        Assert.NotNull(session);

        var pruneTarget = Path.GetFullPath(snapshot.WorktreePath);
        Assert.True(Directory.Exists(pruneTarget));

        var mirrorService = _services.GetRequiredService<WorkspaceLiveMirrorService>();
        var (_, otherLease, otherWt) = await SeedLeaseAsync(sessionId, "prune-trigger", ExecutionLeaseStatus.Merged);
        await File.WriteAllTextAsync(Path.Combine(otherWt, "t.txt"), "t");
        await mirrorService.RefreshForLeaseAsync(otherLease);
        await mirrorService.CompleteMirrorForLeaseAsync(otherLease, WorkerMergeState.Merged);

        Assert.True(Directory.Exists(pruneTarget));
        using var after = JsonDocument.Parse(await File.ReadAllTextAsync(metaPath));
        Assert.Equal("merge_conflict", after.RootElement.GetProperty("mergeState").GetString());
    }

    private async Task AttachPassingVerificationAsync(ExecutionLease lease, IReadOnlyList<string>? files = null)
    {
        lease.VerificationReportJson = VerificationReportSerializer.Serialize(new VerificationReport
        {
            CardId = lease.WorkTaskId,
            SessionId = lease.OperatorSessionId,
            ChangedFiles = files ?? ["src/file.cs"],
            CommandsRun = [new CommandRunSummary { Command = "dotnet test", Passed = true, Summary = "ok" }],
            ReadyForHumanReview = true,
        });
        await _services.GetRequiredService<IExecutionLeaseRepository>().UpdateAsync(lease);
    }

    private async Task<Guid> SeedSessionAsync()
    {
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var id = Guid.NewGuid();
        await sessions.CreateAsync(new OperatorSession
        {
            Id = id,
            Name = "merge-obs",
            WorkspaceRoot = _sessionRoot,
            WorkspaceKey = WorkspacePaths.Normalize(_sessionRoot),
            Status = SessionStatus.Executing,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        return id;
    }

    private async Task<(Guid TaskId, ExecutionLease Lease, string Worktree)> SeedLeaseAsync(
        Guid sessionId,
        string title,
        ExecutionLeaseStatus status)
    {
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
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

        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = taskId,
            OperatorSessionId = sessionId,
            AssignedSessionId = sessionId,
            ExecutionSessionId = Guid.NewGuid(),
            WorktreePath = worktree,
            BranchName = "jz/test",
            Status = status,
            StartedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
        };
        await leases.CreateAsync(lease);
        return (taskId, lease, worktree);
    }
}
