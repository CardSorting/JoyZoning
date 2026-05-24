using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Orchestration;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class OperatorSessionRepository : IOperatorSessionRepository
{
    private readonly JoyZoningDbContext _db;

    public OperatorSessionRepository(JoyZoningDbContext db) => _db = db;

    public Task<OperatorSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.OperatorSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<OperatorSession?> FindByWorkspaceRootAsync(
        string normalizedWorkspaceRoot,
        CancellationToken cancellationToken = default)
    {
        // SQLite cannot ORDER BY DateTimeOffset — filter in SQL, sort client-side.
        var byKeyRows = await _db.OperatorSessions.AsNoTracking()
            .Where(s => s.WorkspaceKey == normalizedWorkspaceRoot)
            .ToListAsync(cancellationToken);
        var byKey = byKeyRows.OrderByDescending(s => s.UpdatedAt).FirstOrDefault();
        if (byKey is not null)
            return byKey;

        var rows = await _db.OperatorSessions.AsNoTracking().ToListAsync(cancellationToken);
        return rows
            .Where(s => WorkspacePaths.EqualsNormalized(s.WorkspaceRoot, normalizedWorkspaceRoot))
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<OperatorSession>> ListAsync(CancellationToken cancellationToken = default)
    {
        // SQLite cannot ORDER BY DateTimeOffset — sort client-side after load.
        var rows = await _db.OperatorSessions.AsNoTracking().ToListAsync(cancellationToken);
        return rows.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    public async Task<IReadOnlyList<OperatorSession>> ListByDeliveryChainIdAsync(
        Guid deliveryChainId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.OperatorSessions.AsNoTracking()
            .Where(s => s.DeliveryChainId == deliveryChainId)
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(s => s.DeliverySequence)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<OperatorSession> CreateAsync(OperatorSession session, CancellationToken cancellationToken = default)
    {
        _db.OperatorSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task UpdateAsync(OperatorSession session, CancellationToken cancellationToken = default)
    {
        var tracked = await _db.OperatorSessions.FindAsync(new object[] { session.Id }, cancellationToken);
        if (tracked is null) return;

        tracked.Name = session.Name;
        tracked.WorkspaceRoot = session.WorkspaceRoot;
        tracked.WorkspaceKey = session.WorkspaceKey;
        tracked.HermesProfile = session.HermesProfile;
        tracked.HermesSessionId = session.HermesSessionId;
        tracked.ActiveTaskId = session.ActiveTaskId;
        tracked.Status = session.Status;
        tracked.ExecutionMode = session.ExecutionMode;
        tracked.DeliveryChainId = session.DeliveryChainId;
        tracked.DeliverySequence = session.DeliverySequence;
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tracked = await _db.OperatorSessions.FindAsync(new object[] { id }, cancellationToken);
        if (tracked is null)
            return;

        _db.OperatorSessions.Remove(tracked);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
