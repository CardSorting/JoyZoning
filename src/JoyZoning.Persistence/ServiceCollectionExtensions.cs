using JoyZoning.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JoyZoning.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJoyZoningPersistence(
        this IServiceCollection services,
        string databasePath)
    {
        var connectionString = databasePath.Contains(';', StringComparison.Ordinal)
            ? databasePath
            : $"Data Source={databasePath};Default Timeout=30;Pooling=True";

        services.AddDbContext<JoyZoningDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IOperatorSessionRepository, OperatorSessionRepository>();
        services.AddScoped<IWorkTaskRepository, WorkTaskRepository>();
        services.AddScoped<IApprovalRepository, ApprovalRepository>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();
        services.AddScoped<IConfigRepository, ConfigRepository>();
        services.AddScoped<IApprovalGrantRepository, ApprovalGrantRepository>();
        services.AddScoped<IExecutionLeaseRepository, ExecutionLeaseRepository>();

        return services;
    }

    public static void EnsureDatabaseCreated(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JoyZoningDbContext>();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS execution_leases (
                Id TEXT NOT NULL PRIMARY KEY,
                WorkTaskId TEXT NOT NULL,
                OperatorSessionId TEXT NOT NULL,
                ExecutionSessionId TEXT NULL,
                WorktreePath TEXT NOT NULL,
                BranchName TEXT NOT NULL,
                Status INTEGER NOT NULL,
                RiskLevel INTEGER NOT NULL,
                AllowedPathsJson TEXT NOT NULL,
                ForbiddenPathsJson TEXT NOT NULL,
                HandoffPacketJson TEXT NOT NULL,
                VerificationReportJson TEXT NULL,
                EvidenceLogJson TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                RevokedAt TEXT NULL,
                MergedAt TEXT NULL,
                BlockedReason TEXT NULL,
                CriticalApprovalGranted INTEGER NOT NULL DEFAULT 0,
                CriticalApprovalConsumed INTEGER NOT NULL DEFAULT 0,
                FailedVerificationReportJson TEXT NULL,
                FOREIGN KEY (WorkTaskId) REFERENCES work_tasks(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_execution_leases_WorkTaskId ON execution_leases (WorkTaskId);
            CREATE INDEX IF NOT EXISTS IX_execution_leases_WorkTaskId_Status ON execution_leases (WorkTaskId, Status);
            """);

        TryAddColumn(db, "execution_leases", "BlockedReason", "TEXT NULL");
        TryAddColumn(db, "execution_leases", "CriticalApprovalGranted", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "execution_leases", "CriticalApprovalConsumed", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "execution_leases", "FailedVerificationReportJson", "TEXT NULL");
        TryAddColumn(db, "execution_leases", "LastHeartbeatAt", "TEXT NULL");
        TryAddColumn(db, "execution_leases", "CreatedBy", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "execution_leases", "AssignedSessionId", "TEXT NULL");
        TryAddColumn(db, "execution_leases", "AssignedAgent", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "execution_leases", "DispatchAttemptCount", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "execution_leases", "RecoveredFromLeaseId", "TEXT NULL");
        TryAddColumn(db, "operator_sessions", "WorkspaceKey", "TEXT NULL");
        TryAddColumn(db, "execution_sessions", "HermesSessionId", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "KanbanRevision", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "work_tasks", "KanbanPushedRevision", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "operator_sessions", "ExecutionMode", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "operator_sessions", "DeliveryChainId", "TEXT NULL");
        TryAddColumn(db, "operator_sessions", "DeliverySequence", "INTEGER NULL");
        TryAddColumn(db, "work_tasks", "TaskExecutionMode", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "work_tasks", "ExecutionDriver", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "work_tasks", "ExternalAgentName", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "BranchName", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "WorkspacePath", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "StartedExternallyAt", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "ReadyForReviewAt", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "LastWorkspaceScanAt", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "LastObservedCommit", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "HasUncommittedChanges", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "work_tasks", "ChangedFilesJson", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "GeneratedPromptPath", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "GeneratedPromptText", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "VerificationRequired", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "work_tasks", "ExternalVerificationStatus", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "work_tasks", "ExternalVerificationReportJson", "TEXT NULL");
        TryAddColumn(db, "work_tasks", "MergeRequired", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "work_tasks", "ExternalMergeCompleted", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "work_tasks", "ExternalMergedAt", "TEXT NULL");

        db.Database.ExecuteSqlRaw("""
             CREATE UNIQUE INDEX IF NOT EXISTS UX_execution_leases_one_active_per_card
  ON execution_leases (WorkTaskId)
  WHERE Status IN (0, 1, 2, 3, 4);
  CREATE UNIQUE INDEX IF NOT EXISTS UX_work_tasks_session_kanban
  ON work_tasks (OperatorSessionId, HermesKanbanTaskId)
  WHERE HermesKanbanTaskId IS NOT NULL;
  CREATE UNIQUE INDEX IF NOT EXISTS UX_operator_sessions_workspace_key
  ON operator_sessions (WorkspaceKey)
  WHERE WorkspaceKey IS NOT NULL;
  """);
    }

    private static void TryAddColumn(JoyZoningDbContext db, string table, string column, string definition)
    {
        try
        {
            db.Database.ExecuteSqlRaw(
                $"ALTER TABLE {table} ADD COLUMN {column} {definition};");
        }
        catch
        {
            // column already exists
        }
    }
}
