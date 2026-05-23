using JoyZoning.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Tests.Infrastructure;

/// <summary>
/// Clears test data without deleting the SQLite file — safe while WebApplicationFactory holds connections open.
/// </summary>
internal static class TestDatabaseReset
{
    public static async Task ClearAllAsync(JoyZoningDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;", cancellationToken);
        try
        {
            await db.ExecutionSteps.ExecuteDeleteAsync(cancellationToken);
            await db.ExecutionSessions.ExecuteDeleteAsync(cancellationToken);
            await db.ExecutionLeases.ExecuteDeleteAsync(cancellationToken);
            await db.ApprovalGrants.ExecuteDeleteAsync(cancellationToken);
            await db.ApprovalRequests.ExecuteDeleteAsync(cancellationToken);
            await db.FileSnapshots.ExecuteDeleteAsync(cancellationToken);
            await db.TerminalSessions.ExecuteDeleteAsync(cancellationToken);
            await db.JoyEvents.ExecuteDeleteAsync(cancellationToken);
            await db.WorkTasks.ExecuteDeleteAsync(cancellationToken);
            await db.OperatorSessions.ExecuteDeleteAsync(cancellationToken);
            await db.Config.ExecuteDeleteAsync(cancellationToken);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);
        }
    }
}
