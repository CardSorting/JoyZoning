using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class ExecutionRepository : IExecutionRepository
{
    private readonly JoyZoningDbContext _db;

    public ExecutionRepository(JoyZoningDbContext db) => _db = db;

    public Task<ExecutionSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ExecutionSessions.AsNoTracking()
            .Include(e => e.Steps)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<ExecutionSession?> GetByRunIdAsync(string hermesRunId, CancellationToken cancellationToken = default) =>
        _db.ExecutionSessions.AsNoTracking().FirstOrDefaultAsync(e => e.HermesRunId == hermesRunId, cancellationToken);

    public async Task<ExecutionSession> CreateAsync(ExecutionSession session, CancellationToken cancellationToken = default)
    {
        _db.ExecutionSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task UpdateHermesSessionIdAsync(
        Guid id,
        string hermesSessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ExecutionSessions.FindAsync(new object[] { id }, cancellationToken);
        if (session is null
            || string.Equals(session.HermesSessionId, hermesSessionId, StringComparison.Ordinal))
            return;

        session.HermesSessionId = hermesSessionId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePhaseAsync(Guid id, ExecutionPhase phase, CancellationToken cancellationToken = default)
    {
        var session = await _db.ExecutionSessions.FindAsync(new object[] { id }, cancellationToken);
        if (session is null) return;

        session.Phase = phase;
        if (phase is ExecutionPhase.Completed or ExecutionPhase.Failed or ExecutionPhase.Cancelled)
            session.EndedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExecutionSession>> ListInterruptedAsync(CancellationToken cancellationToken = default) =>
        await _db.ExecutionSessions.AsNoTracking()
            .Where(e => e.Phase == ExecutionPhase.Interrupted)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExecutionSession>> ListRecoverableAsync(CancellationToken cancellationToken = default) =>
        await ListInterruptedAsync(cancellationToken);
}
