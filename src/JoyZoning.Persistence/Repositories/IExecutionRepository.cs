using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Persistence.Repositories;

public interface IExecutionRepository
{
    Task<ExecutionSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ExecutionSession?> GetByRunIdAsync(string hermesRunId, CancellationToken cancellationToken = default);
    Task<ExecutionSession> CreateAsync(ExecutionSession session, CancellationToken cancellationToken = default);
    Task UpdatePhaseAsync(Guid id, ExecutionPhase phase, CancellationToken cancellationToken = default);
    Task UpdateHermesSessionIdAsync(Guid id, string hermesSessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionSession>> ListInterruptedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionSession>> ListRecoverableAsync(CancellationToken cancellationToken = default);
}
