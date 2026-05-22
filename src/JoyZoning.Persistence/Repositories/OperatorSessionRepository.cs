using JoyZoning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class OperatorSessionRepository : IOperatorSessionRepository
{
    private readonly JoyZoningDbContext _db;

    public OperatorSessionRepository(JoyZoningDbContext db) => _db = db;

    public Task<OperatorSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.OperatorSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OperatorSession>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.OperatorSessions.AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);

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
        tracked.HermesProfile = session.HermesProfile;
        tracked.HermesSessionId = session.HermesSessionId;
        tracked.ActiveTaskId = session.ActiveTaskId;
        tracked.Status = session.Status;
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
