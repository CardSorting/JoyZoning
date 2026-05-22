using JoyZoning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class ConfigRepository : IConfigRepository
{
    private readonly JoyZoningDbContext _db;

    public ConfigRepository(JoyZoningDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var entry = await _db.Config.AsNoTracking().FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
        return entry?.ValueJson;
    }

    public async Task SetAsync(string key, string valueJson, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Config.FindAsync(new object[] { key }, cancellationToken);
        if (existing is null)
        {
            _db.Config.Add(new AppConfigEntry
            {
                Key = key,
                ValueJson = valueJson,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.ValueJson = valueJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Config.AsNoTracking().ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.Key, r => r.ValueJson);
    }
}
