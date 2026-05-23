using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Persistence.Repositories;

public interface IWorkTaskRepository
{
    Task<WorkTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkTask?> GetByHermesKanbanIdAsync(
        Guid sessionId,
        string hermesKanbanTaskId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkTask>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkTask>> ListByWorkspaceRootAsync(
        string workspaceRoot,
        CancellationToken cancellationToken = default);
    Task<WorkTask?> FindByTitleForWorkspaceAsync(
        string workspaceRoot,
        string title,
        CancellationToken cancellationToken = default);
    Task<WorkTask> CreateAsync(WorkTask task, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid id, WorkTaskStatus status, CancellationToken cancellationToken = default);
    Task UpdateDispatchAsync(
        Guid id,
        string linkedRunId,
        WorkTaskStatus status,
        CancellationToken cancellationToken = default);
    Task UpdateAsync(WorkTask task, CancellationToken cancellationToken = default);
}
