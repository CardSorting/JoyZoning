using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>JSDP convergence gate — prior role must be accept-merged, not merely marked Complete.</summary>
public static class JsdpMergeGate
{
    public const string ConvergenceRequiredCode = "jsdp_convergence_required";

    public static bool IsRoleConverged(WorkTask? task, IReadOnlyList<ExecutionLease> taskLeases)
    {
        if (task is null)
            return false;

        if (task.TaskExecutionMode == TaskExecutionMode.ExternalAgent)
        {
            return task.Status == WorkTaskStatus.Complete && task.ExternalMergeCompleted;
        }

        return task.Status == WorkTaskStatus.Complete
            && taskLeases.Any(l => l.WorkTaskId == task.Id && l.Status == ExecutionLeaseStatus.Merged);
    }

    public static IReadOnlyList<ExecutionLease> LeasesForTask(
        Guid taskId,
        IReadOnlyList<ExecutionLease> chainLeases) =>
        chainLeases.Where(l => l.WorkTaskId == taskId).ToList();

    public static string? ValidatePriorRoleConvergence(
        WorkTask? priorTask,
        int priorSequence,
        IReadOnlyList<ExecutionLease> chainLeases,
        int? nextSequence = null)
    {
        if (priorTask is null)
        {
            return $"JSDP merge gate closed: prior role session (sequence {priorSequence}) has no task.";
        }

        var nextHint = nextSequence.HasValue
            ? $"dispatching sequence {nextSequence.Value}"
            : "dispatching the next role";

        if (priorTask.Status != WorkTaskStatus.Complete)
        {
            return $"JSDP merge gate closed: prior role '{priorTask.Title}' (sequence {priorSequence}) "
                + $"is {priorTask.Status}. Accept-merge upstream role before {nextHint}.";
        }

        if (!IsRoleConverged(priorTask, chainLeases))
        {
            return $"JSDP merge gate closed: prior role '{priorTask.Title}' (sequence {priorSequence}) "
                + "is marked Complete but was not accept-merged. Use accept-merge (jz task complete), "
                + $"not direct status changes, before {nextHint}.";
        }

        return null;
    }
}
