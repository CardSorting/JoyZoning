using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Persistence.Repositories;

public interface IApprovalGrantRepository
{
    Task<bool> HasActiveGrantAsync(Guid workTaskId, ApprovalCategory category, CancellationToken cancellationToken = default);
    Task<ApprovalGrant> CreateAsync(ApprovalGrant grant, CancellationToken cancellationToken = default);
}
