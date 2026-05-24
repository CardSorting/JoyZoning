using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Resolves canonical session workspace for JSDP task file inspection.</summary>
public static class WorkspaceInspection
{
    public sealed record InspectionTarget(string Root, bool IsWorktree, Guid SessionId);

    public static async Task<InspectionTarget?> ResolveForTaskAsync(
        Guid taskId,
        IWorkTaskRepository tasks,
        IOperatorSessionRepository sessions,
        KanbanExecutionOrchestrator exec,
        CancellationToken cancellationToken = default)
    {
        var task = await tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
            return null;

        var session = await sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.WorkspaceRoot))
            return null;

        var lease = await exec.GetActiveLeaseAsync(taskId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(lease?.WorktreePath))
        {
            var isIsolated = !JsdpWorkspaceExecution.IsCanonicalWorktree(
                session.WorkspaceRoot,
                lease.WorktreePath);
            return new InspectionTarget(lease.WorktreePath, isIsolated, task.OperatorSessionId);
        }

        return new InspectionTarget(session.WorkspaceRoot, false, task.OperatorSessionId);
    }
}
