using JoyZoning.Domain.Entities;

namespace JoyZoning.Persistence.Repositories;

public interface IOperatorSessionRepository
{
    Task<OperatorSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperatorSession>> ListAsync(CancellationToken cancellationToken = default);
    Task<OperatorSession> CreateAsync(OperatorSession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(OperatorSession session, CancellationToken cancellationToken = default);
}
