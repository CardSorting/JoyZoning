using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class ApprovalRepository : IApprovalRepository
{
    private readonly JoyZoningDbContext _db;

    public ApprovalRepository(JoyZoningDbContext db) => _db = db;

    public Task<ApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.ApprovalRequests.AsNoTracking()
            .Where(a => a.Status == ApprovalStatus.Pending)
            .ToListAsync(cancellationToken);
        return rows.OrderBy(a => a.RequestedAt).ToList();
    }

    public async Task<ApprovalRequest> CreateAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        _db.ApprovalRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task ResolveAsync(
        Guid id,
        ApprovalStatus status,
        ApprovalScope? scope,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.ApprovalRequests.FindAsync(new object[] { id }, cancellationToken);
        if (request is null) return;

        request.Status = status;
        request.GrantedScope = scope;
        request.ResolvedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
