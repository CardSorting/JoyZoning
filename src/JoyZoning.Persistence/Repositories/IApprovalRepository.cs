using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Persistence.Repositories;

public interface IApprovalRepository
{
    Task<ApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalRequest>> ListPendingAsync(CancellationToken cancellationToken = default);
    Task<bool> HasPendingForRunAsync(string hermesRunId, CancellationToken cancellationToken = default);
    Task<ApprovalRequest> CreateAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
    Task ResolveAsync(Guid id, ApprovalStatus status, ApprovalScope? scope, CancellationToken cancellationToken = default);
}
