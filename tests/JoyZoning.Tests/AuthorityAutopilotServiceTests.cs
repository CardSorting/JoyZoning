using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Persistence;
using JoyZoning.Persistence.Repositories;
using JoyZoning.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class AuthorityAutopilotServiceTests : IDisposable
{
    private readonly string _sessionRoot;
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public AuthorityAutopilotServiceTests()
    {
        _sessionRoot = Path.Combine(Path.GetTempPath(), "jz-auth-" + Guid.NewGuid().ToString("N"));
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
        services.Configure<AuthorityOptions>(o =>
        {
            o.AutopilotEnabled = true;
            o.DefaultProfile = AuthorityProfileKind.BalancedAuto;
            o.ReconcileReadyForReview = true;
        });
        services.Configure<LeaseRuntimeOptions>(o => o.MetadataOnlyAcceptResult = false);
        services.Configure<WorkspaceParallelismOptions>(o => o.LargeChangeSetFileThreshold = 20);
        services.AddLogging();
        services.AddScoped<WorkerMergeObservabilityBuilder>();
        services.AddScoped<AuthorityAutopilotMergeContextBuilder>();
        services.AddScoped<AuthorityAutopilotService>();

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
    public async Task TryAutoAccept_blocks_overlapping_ready_workers_and_records_auto_blocked()
    {
        var sessionId = await SeedSessionAsync();
        var leaseA = await SeedReadyLeaseAsync(sessionId, "overlap-a", ["src/shared.cs"]);
        var leaseB = await SeedReadyLeaseAsync(sessionId, "overlap-b", ["src/shared.cs", "src/other.cs"]);

        var autopilot = _services.GetRequiredService<AuthorityAutopilotService>();
        var attempt = await autopilot.TryAutoAcceptAsync(
            leaseA.WorkTaskId,
            () => throw new InvalidOperationException("accept should not run"),
            CancellationToken.None);

        Assert.Equal("blocked", attempt.Outcome);
        Assert.NotNull(attempt.Decision);
        Assert.Contains(AuthorityReasonCodes.OverlappingReadyWorker, attempt.Decision!.ReasonCodes);

        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var updated = await leases.GetByIdAsync(leaseA.Id);
        Assert.Contains("authority.auto_blocked", updated!.EvidenceLogJson, StringComparison.Ordinal);
        Assert.Contains("overlappingPaths", updated.EvidenceLogJson, StringComparison.Ordinal);
        Assert.Equal(ExecutionLeaseStatus.ReadyForReview, updated.Status);

        _ = leaseB;
    }

    [Fact]
    public async Task TryAutoAccept_does_not_duplicate_evidence_on_second_identical_evaluation()
    {
        var sessionId = await SeedSessionAsync("conservative-dedupe");
        _services.GetRequiredService<IOptions<AuthorityOptions>>().Value
            .SessionProfileOverrides["conservative-dedupe"] = AuthorityProfileKind.Conservative;
        var lease = await SeedReadyLeaseAsync(
            sessionId,
            "dedupe",
            ["docs/note.md"],
            sessionName: "conservative-dedupe");

        var autopilot = _services.GetRequiredService<AuthorityAutopilotService>();
        var first = await autopilot.TryAutoAcceptAsync(
            lease.WorkTaskId,
            () => throw new InvalidOperationException("accept should not run"),
            CancellationToken.None);
        Assert.Equal("blocked", first.Outcome);

        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var afterFirst = await leases.GetByIdAsync(lease.Id);
        var countAfterFirst = CountAuthorityEntries(afterFirst!.EvidenceLogJson);

        _ = await autopilot.TryAutoAcceptAsync(
            lease.WorkTaskId,
            () => throw new InvalidOperationException("accept should not run"),
            CancellationToken.None);

        var afterSecond = await leases.GetByIdAsync(lease.Id);
        Assert.Equal(countAfterFirst, CountAuthorityEntries(afterSecond!.EvidenceLogJson));
    }

    [Fact]
    public async Task Reconcile_evaluates_stale_ready_for_review_without_accepting_conservative()
    {
        var sessionId = await SeedSessionAsync(sessionName: "conservative-session");
        var lease = await SeedReadyLeaseAsync(sessionId, "stale", ["docs/note.md"], sessionName: "conservative-session");

        var options = _services.GetRequiredService<IOptions<AuthorityOptions>>();
        options.Value.DefaultProfile = AuthorityProfileKind.Conservative;
        options.Value.SessionProfileOverrides["conservative-session"] = AuthorityProfileKind.Conservative;

        var autopilot = _services.GetRequiredService<AuthorityAutopilotService>();
        var report = await autopilot.ReconcileReadyForReviewAsync(
            (_, _) => throw new InvalidOperationException("accept should not run"),
            sessionId,
            CancellationToken.None);

        Assert.Equal(1, report.Evaluated);
        Assert.Equal(1, report.Blocked);

        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var updated = await leases.GetByIdAsync(lease.Id);
        Assert.Contains("authority.decision", updated!.EvidenceLogJson, StringComparison.Ordinal);
        Assert.Contains("authority.auto_blocked", updated.EvidenceLogJson, StringComparison.Ordinal);
    }

    private async Task<Guid> SeedSessionAsync(string sessionName = "test")
    {
        var sessions = _services.GetRequiredService<IOperatorSessionRepository>();
        var now = DateTimeOffset.UtcNow;
        var session = new OperatorSession
        {
            Id = Guid.NewGuid(),
            Name = sessionName,
            WorkspaceRoot = _sessionRoot,
            Status = SessionStatus.Idle,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await sessions.CreateAsync(session);
        return session.Id;
    }

    private async Task<ExecutionLease> SeedReadyLeaseAsync(
        Guid sessionId,
        string title,
        IReadOnlyList<string> changedFiles,
        string sessionName = "test")
    {
        var tasks = _services.GetRequiredService<IWorkTaskRepository>();
        var leases = _services.GetRequiredService<IExecutionLeaseRepository>();
        var now = DateTimeOffset.UtcNow;
        var wt = Path.Combine(_sessionRoot, ".joyzoning", "worktrees", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(wt);

        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            OperatorSessionId = sessionId,
            Title = title,
            Description = "test",
            Risk = RiskLevel.Low,
            Status = WorkTaskStatus.NeedsApproval,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await tasks.CreateAsync(task);

        var report = new VerificationReport
        {
            CardId = task.Id,
            SessionId = sessionId,
            ChangedFiles = changedFiles,
            CommandsRun = [new CommandRunSummary { Command = "echo ok", Passed = true, Summary = "ok" }],
            ReadyForHumanReview = true,
        };

        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = task.Id,
            OperatorSessionId = sessionId,
            AssignedSessionId = sessionId,
            WorktreePath = wt,
            BranchName = "worker/" + task.Id.ToString("N")[..8],
            Status = ExecutionLeaseStatus.ReadyForReview,
            VerificationReportJson = VerificationReportSerializer.Serialize(report),
            EvidenceLogJson = "[]",
            StartedAt = now,
            ExpiresAt = now.AddHours(8),
            LastHeartbeatAt = now,
        };
        await leases.CreateAsync(lease);
        _ = sessionName;
        return lease;
    }

    private static int CountAuthorityEntries(string? evidenceLogJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceLogJson))
            return 0;

        return LeaseEvidenceLog.DeserializeEntries(evidenceLogJson)
            .Count(e => e.Kind.StartsWith("authority.", StringComparison.Ordinal));
    }
}
