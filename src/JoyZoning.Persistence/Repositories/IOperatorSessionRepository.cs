using JoyZoning.Domain.Entities;

namespace JoyZoning.Persistence.Repositories;

public interface IOperatorSessionRepository
{
    Task<OperatorSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperatorSession?> FindByWorkspaceRootAsync(
        string normalizedWorkspaceRoot,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperatorSession>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperatorSession>> ListByDeliveryChainIdAsync(
        Guid deliveryChainId,
        CancellationToken cancellationToken = default);
    Task<OperatorSession> CreateAsync(OperatorSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(OperatorSession session, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
