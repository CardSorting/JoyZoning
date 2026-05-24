using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>
/// Enforces single bounded agent + single bounded role per operator session.
/// Multi-agent parallel throughput is intentionally rejected — it does not converge.
/// </summary>
public static class BoundedSessionGate
{
    public const string ModelName = "bounded_session";

    private static readonly WorkTaskStatus[] InFlightTaskStatuses =
    [
        WorkTaskStatus.InProgress,
        WorkTaskStatus.Verifying,
        WorkTaskStatus.NeedsApproval,
        WorkTaskStatus.Blocked,
    ];

    public sealed record PlanEntry(
        Guid TaskId,
        string Title,
        DeliveryRoleKind Role,
        WorkTaskStatus Status,
        bool Dispatchable,
        string? BlockReason);

    public sealed record Plan(
        Guid SessionId,
        string SessionWorkspaceRoot,
        string Model,
        int MaxActiveLeasesPerSession,
        int ActiveLeaseCount,
        Guid? InFlightTaskId,
        string? InFlightTaskTitle,
        WorkTaskStatus? InFlightTaskStatus,
        bool HasFoundation,
        Guid? NextDispatchableTaskId,
        IReadOnlyList<PlanEntry> Tasks);

    public static string? ValidateDispatch(
        OperatorSession session,
        WorkTask task,
        IReadOnlyList<WorkTask> sessionTasks,
        IReadOnlyList<ExecutionLease> activeSessionLeases,
        LeaseRuntimeOptions options)
    {
        if (options.MaxActiveLeasesPerSession < 1)
            return "Scheduler policy disables session leases.";

        if (session.IsBoundedRoleSession && sessionTasks.Count > 1)
        {
            return "Bounded role session allows exactly one task. "
                + "Use a separate session per role in the delivery chain.";
        }

        var otherActiveLeases = activeSessionLeases
            .Where(l => l.WorkTaskId != task.Id && KanbanExecutionRules.IsActiveLease(l))
            .ToList();

        if (otherActiveLeases.Count >= options.MaxActiveLeasesPerSession)
        {
            var occupant = otherActiveLeases[0];
            return $"Single-agent session: lease {occupant.Status} on another task is active. "
                + "Finish, accept-merge, or revoke before dispatching another role.";
        }

        var inFlight = sessionTasks
            .Where(t => t.Id != task.Id && InFlightTaskStatuses.Contains(t.Status))
            .OrderBy(t => t.UpdatedAt)
            .ToList();

        if (inFlight.Count > 0)
        {
            var peer = inFlight[0];
            return $"Single-role session: '{peer.Title}' is {peer.Status}. "
                + "Accept-merge or recover to completion before dispatching the next role.";
        }

        if (task.Status is WorkTaskStatus.Complete)
            return "Task is already complete.";

        if (task.Status is not (WorkTaskStatus.Backlog or WorkTaskStatus.Planned))
        {
            return $"Task status {task.Status} is not dispatchable in a bounded session. "
                + "Recover or reopen the task first.";
        }

        return null;
    }

    public static Plan BuildPlan(
        OperatorSession session,
        IReadOnlyList<WorkTask> sessionTasks,
        IReadOnlyList<ExecutionLease> activeSessionLeases,
        LeaseRuntimeOptions options)
    {
        var activeCount = activeSessionLeases.Count(KanbanExecutionRules.IsActiveLease);
        var inFlight = sessionTasks
            .Where(t => InFlightTaskStatuses.Contains(t.Status))
            .OrderBy(t => t.UpdatedAt)
            .FirstOrDefault();

        var entries = new List<PlanEntry>();
        Guid? nextDispatchable = null;

        foreach (var task in sessionTasks.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase))
        {
            var blockReason = ValidateDispatch(session, task, sessionTasks, activeSessionLeases, options);
            var dispatchable = blockReason is null;
            if (dispatchable && nextDispatchable is null)
                nextDispatchable = task.Id;

            entries.Add(new PlanEntry(
                task.Id,
                task.Title,
                DeliveryRoleClassifier.Classify(task),
                task.Status,
                dispatchable,
                blockReason));
        }

        return new Plan(
            session.Id,
            session.WorkspaceRoot,
            ModelName,
            options.MaxActiveLeasesPerSession,
            activeCount,
            inFlight?.Id,
            inFlight?.Title,
            inFlight?.Status,
            WorktreeSeeder.HasProjectFoundation(session.WorkspaceRoot),
            nextDispatchable,
            entries);
    }
}
