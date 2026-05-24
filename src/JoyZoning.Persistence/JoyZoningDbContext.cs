using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence;

public class JoyZoningDbContext : DbContext
{
    public JoyZoningDbContext(DbContextOptions<JoyZoningDbContext> options)
        : base(options)
    {
    }

    public DbSet<OperatorSession> OperatorSessions => Set<OperatorSession>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<ExecutionSession> ExecutionSessions => Set<ExecutionSession>();
    public DbSet<ExecutionStep> ExecutionSteps => Set<ExecutionStep>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<JoyEvent> JoyEvents => Set<JoyEvent>();
    public DbSet<FileSnapshot> FileSnapshots => Set<FileSnapshot>();
    public DbSet<TerminalSession> TerminalSessions => Set<TerminalSession>();
    public DbSet<AppConfigEntry> Config => Set<AppConfigEntry>();
    public DbSet<ApprovalGrant> ApprovalGrants => Set<ApprovalGrant>();
    public DbSet<ExecutionLease> ExecutionLeases => Set<ExecutionLease>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperatorSession>(e =>
        {
            e.ToTable("operator_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.WorkspaceRoot).HasMaxLength(2048).IsRequired();
            e.Property(x => x.WorkspaceKey).HasMaxLength(2048);
            e.HasIndex(x => x.WorkspaceKey).IsUnique();
            e.Property(x => x.HermesProfile).HasMaxLength(128);
            e.Property(x => x.HermesSessionId).HasMaxLength(128);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DeliveryChainId);
            e.Property(x => x.ExecutionMode).HasDefaultValue(SessionExecutionMode.Default);
        });

        modelBuilder.Entity<WorkTask>(e =>
        {
            e.ToTable("work_tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(512).IsRequired();
            e.Property(x => x.HermesKanbanTaskId).HasMaxLength(64);
            e.Property(x => x.ExternalAgentName).HasMaxLength(128);
            e.Property(x => x.BranchName).HasMaxLength(256);
            e.Property(x => x.WorkspacePath).HasMaxLength(2048);
            e.Property(x => x.LastObservedCommit).HasMaxLength(128);
            e.Property(x => x.GeneratedPromptPath).HasMaxLength(2048);
            e.Property(x => x.TaskExecutionMode).HasDefaultValue(TaskExecutionMode.ManagedAgent);
            e.Property(x => x.ExecutionDriver).HasDefaultValue(ExecutionDriver.Hermes);
            e.Property(x => x.LinkedRunId).HasMaxLength(128);
            e.HasIndex(x => new { x.OperatorSessionId, x.Status });
            e.HasOne(x => x.OperatorSession)
                .WithMany(s => s.Tasks)
                .HasForeignKey(x => x.OperatorSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExecutionSession>(e =>
        {
            e.ToTable("execution_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.HermesRunId).HasMaxLength(128).IsRequired();
            e.Property(x => x.HermesSessionId).HasMaxLength(128);
            e.HasIndex(x => x.WorkTaskId);
            e.HasOne(x => x.WorkTask)
                .WithMany(t => t.Executions)
                .HasForeignKey(x => x.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExecutionStep>(e =>
        {
            e.ToTable("execution_steps");
            e.HasKey(x => x.Id);
            e.Property(x => x.Label).HasMaxLength(512).IsRequired();
            e.Property(x => x.Status).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.ExecutionSessionId, x.Ordinal });
            e.HasOne(x => x.ExecutionSession)
                .WithMany(es => es.Steps)
                .HasForeignKey(x => x.ExecutionSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.ToTable("approval_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Command).HasMaxLength(4096).IsRequired();
            e.Property(x => x.HermesRunId).HasMaxLength(128);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<JoyEvent>(e =>
        {
            e.ToTable("joy_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Type).HasMaxLength(128).IsRequired();
            e.HasIndex(x => new { x.CorrelationId, x.Id });
            e.HasIndex(x => x.OccurredAt);
        });

        modelBuilder.Entity<FileSnapshot>(e =>
        {
            e.ToTable("file_snapshots");
            e.HasKey(x => x.Id);
            e.Property(x => x.Path).HasMaxLength(2048).IsRequired();
            e.HasIndex(x => x.WorkTaskId);
        });

        modelBuilder.Entity<TerminalSession>(e =>
        {
            e.ToTable("terminal_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Backend).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<AppConfigEntry>(e =>
        {
            e.ToTable("config");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(256);
        });

        modelBuilder.Entity<ApprovalGrant>(e =>
        {
            e.ToTable("approval_grants");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WorkTaskId, x.Category });
            e.HasOne(x => x.WorkTask)
                .WithMany()
                .HasForeignKey(x => x.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExecutionLease>(e =>
        {
            e.ToTable("execution_leases");
            e.HasKey(x => x.Id);
            e.Property(x => x.WorktreePath).HasMaxLength(2048).IsRequired();
            e.Property(x => x.BranchName).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.WorkTaskId);
            e.HasIndex(x => new { x.WorkTaskId, x.Status });
            e.Property(x => x.BlockedReason).HasMaxLength(2048);
            e.HasIndex(x => x.AssignedSessionId);
            e.HasOne(x => x.WorkTask)
                .WithMany()
                .HasForeignKey(x => x.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
