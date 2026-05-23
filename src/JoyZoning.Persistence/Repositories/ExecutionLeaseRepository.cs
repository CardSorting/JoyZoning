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

    public async Task<IReadOnlyList<ExecutionLease>> ListForWorkspaceAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _db.OperatorSessions.AsNoTracking().ToListAsync(cancellationToken);
        var sessionIds = sessions
            .Where(s => WorkspacePaths.EqualsNormalized(s.WorkspaceRoot, workspaceRoot))
            .Select(s => s.Id)
            .ToHashSet();

        if (sessionIds.Count == 0)
            return Array.Empty<ExecutionLease>();

        var taskIds = await _db.WorkTasks.AsNoTracking()
            .Where(t => sessionIds.Contains(t.OperatorSessionId))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (taskIds.Count == 0)
            return Array.Empty<ExecutionLease>();

        var leases = await _db.ExecutionLeases
            .Where(l => taskIds.Contains(l.WorkTaskId))
            .ToListAsync(cancellationToken);

        return leases.OrderByDescending(l => l.StartedAt).ToList();
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

    public async Task<int> CountActiveForWorkspaceAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _db.OperatorSessions.AsNoTracking().ToListAsync(cancellationToken);
        var sessionIds = sessions
            .Where(s => WorkspacePaths.EqualsNormalized(s.WorkspaceRoot, workspaceRoot))
            .Select(s => s.Id)
            .ToHashSet();

        if (sessionIds.Count == 0)
            return 0;

        var taskIds = await _db.WorkTasks.AsNoTracking()
            .Where(t => sessionIds.Contains(t.OperatorSessionId))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (taskIds.Count == 0)
            return 0;

        return await _db.ExecutionLeases.CountAsync(
            l => taskIds.Contains(l.WorkTaskId)
                 && KanbanExecutionRules.ActiveLeaseStatuses.Contains(l.Status),
            cancellationToken);
    }

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

    public async Task ReassignWorkTaskAsync(
        Guid fromWorkTaskId,
        Guid toWorkTaskId,
        CancellationToken cancellationToken = default)
    {
        var leases = await _db.ExecutionLeases
            .Where(l => l.WorkTaskId == fromWorkTaskId)
            .ToListAsync(cancellationToken);

        foreach (var lease in leases)
            lease.WorkTaskId = toWorkTaskId;

        if (leases.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReassignOperatorSessionAsync(
        Guid fromSessionId,
        Guid toSessionId,
        CancellationToken cancellationToken = default)
    {
        var leases = await _db.ExecutionLeases
            .Where(l => l.OperatorSessionId == fromSessionId || l.AssignedSessionId == fromSessionId)
            .ToListAsync(cancellationToken);

        foreach (var lease in leases)
        {
            if (lease.OperatorSessionId == fromSessionId)
                lease.OperatorSessionId = toSessionId;
            if (lease.AssignedSessionId == fromSessionId)
                lease.AssignedSessionId = toSessionId;
        }

        if (leases.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
    }
}
