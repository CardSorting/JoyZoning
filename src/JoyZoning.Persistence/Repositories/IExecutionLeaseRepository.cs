using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Persistence.Repositories;

public interface IExecutionLeaseRepository
{
    Task<ExecutionLease?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExecutionLease?> GetActiveByTaskIdAsync(Guid workTaskId, CancellationToken cancellationToken = default);
    Task<ExecutionLease?> GetGlobalCriticalOccupantAsync(
        Guid? excludingLeaseId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionLease>> ListByTaskIdAsync(
        Guid workTaskId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionLease>> ListActiveAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<int> CountActiveBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<ExecutionLease> CreateAsync(ExecutionLease lease, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExecutionLease lease, CancellationToken cancellationToken = default);
}
