using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>
/// Sequential queue across bounded-role sessions sharing one physical workspace.
/// Role N+1 cannot dispatch until role N is Complete (accept-merged).
/// </summary>
public static class RoleDeliveryChainGate
{
    public const string ModelName = "role_delivery_chain";

    public sealed record JsdpQueueStatus(
        string Protocol,
        int? CurrentRoleSequence,
        string? CurrentRoleTitle,
        string? PreviousRoleTitle,
        string? PreviousRoleStatus,
        string MergeGateStatus,
        string NextDispatchEligibility,
        string? NextHumanAction,
        string? BlockReason);

    public sealed record ChainStep(
        Guid SessionId,
        string SessionName,
        int Sequence,
        Guid? TaskId,
        string? TaskTitle,
        DeliveryRoleKind Role,
        WorkTaskStatus? TaskStatus,
        string? LeaseStatus,
        bool Complete,
        bool ActiveLease,
        bool Dispatchable,
        string MergeGateStatus,
        string? HumanActionRequired,
        string? BlockReason,
        IReadOnlyList<string> JsdpComplianceWarnings);

    public sealed record ChainQueue(
        Guid ChainId,
        string WorkspaceRoot,
        string Model,
        int TotalRoles,
        int CompletedRoles,
        Guid? ActiveSessionId,
        Guid? NextSessionId,
        Guid? NextTaskId,
        string? BlockReason,
        JsdpQueueStatus Jsdp,
        IReadOnlyList<ChainStep> Steps);

    public static string? ValidateDispatch(
        OperatorSession session,
        WorkTask task,
        IReadOnlyList<OperatorSession> chainSessions,
        IReadOnlyList<WorkTask> chainTasks,
        IReadOnlyList<ExecutionLease> chainLeases,
        LeaseRuntimeOptions options)
    {
        if (!session.IsBoundedRoleSession)
            return null;

        var handoffError = JsdpHandoffCompliance.ValidateDispatchHandoffCompliance(task, session);
        if (handoffError is not null)
            return handoffError;

        var lockError = JsdpHandoffCompliance.ValidateLockArtifactsForDispatch(session, session.WorkspaceRoot);
        if (lockError is not null)
            return lockError;

        var chainId = session.DeliveryChainId!.Value;
        var ordered = OrderChain(chainSessions, chainId);
        if (ordered.Count == 0)
            return "JSDP chain blocked: delivery chain session is missing chain peers.";

        var selfIndex = FindSessionIndex(ordered, session.Id);
        if (selfIndex < 0)
            return "JSDP chain blocked: session is not registered in its delivery chain.";

        for (var i = 0; i < selfIndex; i++)
        {
            var priorSession = ordered[i];
            var priorTask = PrimaryTaskForSession(priorSession.Id, chainTasks);
            var convergenceError = JsdpMergeGate.ValidatePriorRoleConvergence(
                priorTask,
                priorSession.DeliverySequence ?? i + 1,
                chainLeases,
                session.DeliverySequence);
            if (convergenceError is not null)
                return convergenceError;
        }

        var chainActiveLeases = chainLeases.Where(KanbanExecutionRules.IsActiveLease).ToList();
        var otherActive = chainActiveLeases
            .Where(l => l.OperatorSessionId != session.Id && KanbanExecutionRules.IsActiveLease(l))
            .ToList();

        if (otherActive.Count > 0)
        {
            return "JSDP chain blocked: another role session has an active lease. "
                + "Finish or revoke it before dispatching the next role.";
        }

        return BoundedSessionGate.ValidateDispatch(
            session,
            task,
            chainTasks.Where(t => t.OperatorSessionId == session.Id).ToList(),
            chainActiveLeases.Where(l => l.OperatorSessionId == session.Id).ToList(),
            options);
    }

    public static ChainQueue BuildQueue(
        Guid chainId,
        string workspaceRoot,
        IReadOnlyList<OperatorSession> chainSessions,
        IReadOnlyList<WorkTask> chainTasks,
        IReadOnlyList<ExecutionLease> chainLeases,
        LeaseRuntimeOptions options)
    {
        var chainActiveLeases = chainLeases.Where(KanbanExecutionRules.IsActiveLease).ToList();
        var ordered = OrderChain(chainSessions, chainId);
        var steps = new List<ChainStep>();
        Guid? nextSessionId = null;
        Guid? nextTaskId = null;
        Guid? activeSessionId = null;

        foreach (var chainSession in ordered)
        {
            var task = PrimaryTaskForSession(chainSession.Id, chainTasks);
            var sessionLeases = chainActiveLeases
                .Where(l => l.OperatorSessionId == chainSession.Id)
                .ToList();
            var activeLease = sessionLeases.FirstOrDefault(KanbanExecutionRules.IsActiveLease);

            string? blockReason = null;
            var dispatchable = false;
            IReadOnlyList<string> warnings = Array.Empty<string>();

            if (task is null)
            {
                blockReason = "Bounded role session requires exactly one task.";
            }
            else
            {
                warnings = JsdpHandoffCompliance.ValidateTaskDescription(task.Description).Warnings;
                blockReason = ValidateDispatch(
                    chainSession, task, chainSessions, chainTasks, chainLeases, options);
                dispatchable = blockReason is null;
            }

            var taskLeases = task is null ? Array.Empty<ExecutionLease>() : JsdpMergeGate.LeasesForTask(task.Id, chainLeases);
            var converged = JsdpMergeGate.IsRoleConverged(task, taskLeases);

            var hasActiveLease = activeLease is not null;
            if (hasActiveLease)
                activeSessionId = chainSession.Id;

            if (dispatchable && nextSessionId is null && task is not null)
            {
                nextSessionId = chainSession.Id;
                nextTaskId = task.Id;
            }

            var mergeGate = MergeGateStatusForStep(task, activeLease, blockReason, converged);
            var humanAction = HumanActionForStep(task, activeLease, dispatchable, blockReason, converged);

            steps.Add(new ChainStep(
                chainSession.Id,
                chainSession.Name,
                chainSession.DeliverySequence ?? 0,
                task?.Id,
                task?.Title,
                task is null ? DeliveryRoleKind.Unknown : DeliveryRoleClassifier.Classify(task),
                task?.Status,
                activeLease?.Status.ToString(),
                converged,
                hasActiveLease,
                dispatchable,
                mergeGate,
                humanAction,
                blockReason,
                warnings));
        }

        var nextStep = nextSessionId.HasValue
            ? steps.FirstOrDefault(s => s.SessionId == nextSessionId)
            : null;
        var currentStep = steps.FirstOrDefault(s => s.ActiveLease)
            ?? steps.FirstOrDefault(s => s.TaskStatus is WorkTaskStatus.InProgress or WorkTaskStatus.Verifying
                or WorkTaskStatus.NeedsApproval or WorkTaskStatus.Blocked or WorkTaskStatus.ExternalInProgress
                or WorkTaskStatus.ReadyForReview or WorkTaskStatus.Verified or WorkTaskStatus.HermesRunning);
        var chainBlockReason = nextStep?.Dispatchable == true
            ? null
            : nextStep?.BlockReason ?? currentStep?.BlockReason;

        var jsdp = BuildJsdpStatus(steps, activeSessionId, nextStep, chainBlockReason);

        return new ChainQueue(
            chainId,
            workspaceRoot,
            ModelName,
            ordered.Count,
            steps.Count(s => s.Complete),
            activeSessionId,
            nextSessionId,
            nextTaskId,
            chainBlockReason,
            jsdp,
            steps);
    }

    private static JsdpQueueStatus BuildJsdpStatus(
        IReadOnlyList<ChainStep> steps,
        Guid? activeSessionId,
        ChainStep? nextStep,
        string? chainBlockReason)
    {
        var current = steps.FirstOrDefault(s => s.ActiveLease)
            ?? steps.FirstOrDefault(s => s.TaskStatus is WorkTaskStatus.InProgress or WorkTaskStatus.Verifying
                or WorkTaskStatus.NeedsApproval or WorkTaskStatus.Blocked or WorkTaskStatus.ExternalInProgress
                or WorkTaskStatus.ReadyForReview or WorkTaskStatus.Verified or WorkTaskStatus.HermesRunning);
        var prior = current is not null
            ? steps.Where(s => s.Sequence < current.Sequence).OrderByDescending(s => s.Sequence).FirstOrDefault()
            : nextStep is not null
                ? steps.Where(s => s.Sequence < nextStep.Sequence).OrderByDescending(s => s.Sequence).FirstOrDefault()
                : null;

        var eligibility = nextStep?.Dispatchable == true
            ? "eligible"
            : steps.All(s => s.Complete)
                ? "complete"
                : "blocked";

        var mergeGate = activeSessionId.HasValue && current?.TaskStatus is WorkTaskStatus.NeedsApproval or WorkTaskStatus.Verifying
            ? "waiting_accept_merge"
            : eligibility == "eligible"
                ? "open"
                : eligibility == "complete"
                    ? "satisfied"
                    : "closed";

        var humanAction = nextStep?.HumanActionRequired
            ?? current?.HumanActionRequired
            ?? (eligibility == "complete" ? "Chain complete — no further roles." : chainBlockReason);

        return new JsdpQueueStatus(
            JsdpProtocol.ProtocolId,
            current?.Sequence ?? nextStep?.Sequence,
            current?.TaskTitle ?? nextStep?.TaskTitle,
            prior?.TaskTitle,
            prior?.TaskStatus?.ToString(),
            mergeGate,
            eligibility,
            humanAction,
            chainBlockReason);
    }

    private static string MergeGateStatusForStep(
        WorkTask? task,
        ExecutionLease? activeLease,
        string? blockReason,
        bool converged)
    {
        if (converged)
            return "satisfied";
        if (activeLease?.Status is ExecutionLeaseStatus.ReadyForReview)
            return "waiting_accept_merge";
        if (blockReason?.Contains("merge gate closed", StringComparison.OrdinalIgnoreCase) == true)
            return "closed";
        if (task?.Status is WorkTaskStatus.InProgress or WorkTaskStatus.Verifying or WorkTaskStatus.NeedsApproval
            or WorkTaskStatus.ExternalInProgress or WorkTaskStatus.ReadyForReview or WorkTaskStatus.Verified)
            return "waiting_accept_merge";
        return blockReason is null ? "open" : "closed";
    }

    private static string? HumanActionForStep(
        WorkTask? task,
        ExecutionLease? activeLease,
        bool dispatchable,
        string? blockReason,
        bool converged)
    {
        if (converged)
            return null;
        if (activeLease?.Status == ExecutionLeaseStatus.ReadyForReview || task?.Status == WorkTaskStatus.NeedsApproval)
            return "Review worker output and accept-merge into canonical workspace.";
        if (task?.Status == WorkTaskStatus.ExternalInProgress)
            return "Edit in external agent, then: jz task mark-ready <task-id>";
        if (task?.Status is WorkTaskStatus.ReadyForReview or WorkTaskStatus.Verified)
            return "Verify and complete: jz task verify <task-id> --cmd \"...\" then jz task complete <task-id> --yes";
        if (activeLease is not null)
            return $"Monitor running lease ({activeLease.Status}) or revoke if stale.";
        if (dispatchable)
            return "Dispatch this role (jz delivery-chain next --external --agent cursor).";
        return blockReason;
    }

    public static IReadOnlyList<OperatorSession> OrderChain(
        IReadOnlyList<OperatorSession> sessions,
        Guid chainId) =>
        sessions
            .Where(s => s.DeliveryChainId == chainId && s.IsBoundedRoleSession)
            .OrderBy(s => s.DeliverySequence)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static int FindSessionIndex(IReadOnlyList<OperatorSession> ordered, Guid sessionId)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Id == sessionId)
                return i;
        }

        return -1;
    }

    private static WorkTask? PrimaryTaskForSession(Guid sessionId, IReadOnlyList<WorkTask> tasks) =>
        tasks
            .Where(t => t.OperatorSessionId == sessionId)
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefault();
}
