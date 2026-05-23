using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JoyZoning.Persistence.Repositories;

public class WorkTaskRepository : IWorkTaskRepository
{
    private readonly JoyZoningDbContext _db;

    public WorkTaskRepository(JoyZoningDbContext db) => _db = db;

    public Task<WorkTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.WorkTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<WorkTask?> GetByHermesKanbanIdAsync(
        Guid sessionId,
        string hermesKanbanTaskId,
        CancellationToken cancellationToken = default) =>
        _db.WorkTasks.FirstOrDefaultAsync(
            t => t.OperatorSessionId == sessionId && t.HermesKanbanTaskId == hermesKanbanTaskId,
            cancellationToken);

    public async Task<IReadOnlyList<WorkTask>> ListBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.WorkTasks.AsNoTracking()
            .Where(t => t.OperatorSessionId == sessionId)
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.UpdatedAt)
            .ToList();
    }

    public async Task<WorkTask> CreateAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        _db.WorkTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task UpdateStatusAsync(Guid id, WorkTaskStatus status, CancellationToken cancellationToken = default)
    {
        var task = await _db.WorkTasks.FindAsync(new object[] { id }, cancellationToken);
        if (task is null) return;

        task.Status = status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        if (status == WorkTaskStatus.Complete)
            task.CompletedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDispatchAsync(
        Guid id,
        string linkedRunId,
        WorkTaskStatus status,
        CancellationToken cancellationToken = default)
    {
        var task = await _db.WorkTasks.FindAsync(new object[] { id }, cancellationToken);
        if (task is null) return;

        task.LinkedRunId = linkedRunId;
        task.Status = status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WorkTask task, CancellationToken cancellationToken = default)
    {
        task.UpdatedAt = DateTimeOffset.UtcNow;
        _db.WorkTasks.Update(task);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
