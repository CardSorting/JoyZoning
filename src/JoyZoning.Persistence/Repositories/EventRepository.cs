using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class EventRepository : IEventRepository
{
    private readonly JoyZoningDbContext _db;

    public EventRepository(JoyZoningDbContext db) => _db = db;

    public async Task<JoyEvent> AppendAsync(
        Guid correlationId,
        EventSource source,
        string type,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var evt = new JoyEvent
        {
            CorrelationId = correlationId,
            Source = source,
            Type = type,
            PayloadJson = payloadJson,
            OccurredAt = DateTimeOffset.UtcNow,
        };
        _db.JoyEvents.Add(evt);
        await _db.SaveChangesAsync(cancellationToken);
        return evt;
    }

    public async Task<IReadOnlyList<JoyEvent>> QueryAsync(
        long? sinceCursor = null,
        Guid? correlationId = null,
        IReadOnlyList<string>? types = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var query = _db.JoyEvents.AsNoTracking().AsQueryable();

        if (sinceCursor.HasValue)
            query = query.Where(e => e.Id > sinceCursor.Value);

        if (correlationId.HasValue)
            query = query.Where(e => e.CorrelationId == correlationId.Value);

        if (types is { Count: > 0 })
            query = query.Where(e => types.Contains(e.Type));

        return await query
            .OrderBy(e => e.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
