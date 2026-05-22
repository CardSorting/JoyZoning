using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class ApprovalGrantRepository : IApprovalGrantRepository
{
    private readonly JoyZoningDbContext _db;

    public ApprovalGrantRepository(JoyZoningDbContext db) => _db = db;

    public async Task<bool> HasActiveGrantAsync(
        Guid workTaskId,
        ApprovalCategory category,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Set<ApprovalGrant>().AnyAsync(
            g => g.WorkTaskId == workTaskId &&
                 g.Category == category &&
                 (g.ExpiresAt == null || g.ExpiresAt > now),
            cancellationToken);
    }

    public async Task<ApprovalGrant> CreateAsync(ApprovalGrant grant, CancellationToken cancellationToken = default)
    {
        _db.Set<ApprovalGrant>().Add(grant);
        await _db.SaveChangesAsync(cancellationToken);
        return grant;
    }
}
