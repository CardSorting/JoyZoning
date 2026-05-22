using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class ExecutionLeaseRepository : IExecutionLeaseRepository
{
    private readonly JoyZoningDbContext _db;

    public ExecutionLeaseRepository(JoyZoningDbContext db) => _db = db;

    public Task<ExecutionLease?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ExecutionLeases.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<ExecutionLease?> GetActiveByTaskIdAsync(
        Guid workTaskId,
        CancellationToken cancellationToken = default)
    {
        var active = await _db.ExecutionLeases
            .Where(l => l.WorkTaskId == workTaskId
                        && KanbanExecutionRules.ActiveLeaseStatuses.Contains(l.Status))
            .ToListAsync(cancellationToken);

        return active.OrderByDescending(l => l.StartedAt).FirstOrDefault();
    }

    public async Task<ExecutionLease?> GetGlobalCriticalOccupantAsync(
        Guid? excludingLeaseId = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = (await _db.ExecutionLeases
            .Where(l => l.RiskLevel == LeaseRiskLevel.Critical)
            .ToListAsync(cancellationToken))
            .OrderByDescending(l => l.StartedAt);

        foreach (var lease in candidates)
        {
            if (excludingLeaseId.HasValue && lease.Id == excludingLeaseId.Value)
                continue;

            if (KanbanExecutionRules.OccupiesGlobalCriticalSlot(lease))
                return lease;
        }

        return null;
    }

    public async Task<IReadOnlyList<ExecutionLease>> ListByTaskIdAsync(
        Guid workTaskId,
        CancellationToken cancellationToken = default)
    {
        var leases = await _db.ExecutionLeases
            .Where(l => l.WorkTaskId == workTaskId)
            .ToListAsync(cancellationToken);

        return leases.OrderByDescending(l => l.StartedAt).ToList();
    }

    public async Task<IReadOnlyList<ExecutionLease>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var leases = await _db.ExecutionLeases
            .Where(l => KanbanExecutionRules.ActiveLeaseStatuses.Contains(l.Status))
            .ToListAsync(cancellationToken);
        return leases;
    }

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        _db.ExecutionLeases.CountAsync(
            l => KanbanExecutionRules.ActiveLeaseStatuses.Contains(l.Status),
            cancellationToken);

    public Task<int> CountActiveBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        _db.ExecutionLeases.CountAsync(
            l => l.AssignedSessionId == sessionId
                 && KanbanExecutionRules.ActiveLeaseStatuses.Contains(l.Status),
            cancellationToken);

    public async Task<ExecutionLease> CreateAsync(ExecutionLease lease, CancellationToken cancellationToken = default)
    {
        var existing = await GetActiveByTaskIdAsync(lease.WorkTaskId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("Card already has an active execution lease.");

        _db.ExecutionLeases.Add(lease);
        await _db.SaveChangesAsync(cancellationToken);
        return lease;
    }

    public async Task UpdateAsync(ExecutionLease lease, CancellationToken cancellationToken = default)
    {
        _db.ExecutionLeases.Update(lease);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
