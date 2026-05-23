using JoyZoning.Agents;
using JoyZoning.ControlPlane.Background;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Heartbeat, expiration, scheduling capacity, recovery, and reconciliation.</summary>
public class LeaseRuntimeService
{
    private readonly IExecutionLeaseRepository _leases;
    private readonly IWorkTaskRepository _tasks;
    private readonly IExecutionRepository _executions;
    private readonly EventIngestor _events;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LeaseRuntimeOptions _options;

    public LeaseRuntimeService(
        IExecutionLeaseRepository leases,
        IWorkTaskRepository tasks,
        IExecutionRepository executions,
        EventIngestor events,
        IServiceScopeFactory scopeFactory,
        IOptions<LeaseRuntimeOptions> options)
    {
        _leases = leases;
        _tasks = tasks;
        _executions = executions;
        _events = events;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    public async Task<string?> ValidateSchedulingForNewLeaseAsync(
        Guid sessionId,
        LeaseRiskLevel risk,
        bool humanApprovedCritical,
        Guid? excludingLeaseId = null,
        CancellationToken cancellationToken = default)
    {
        var globalCount = await _leases.CountActiveAsync(cancellationToken);
        var sessionCount = await _leases.CountActiveBySessionAsync(sessionId, cancellationToken);
        var critical = await _leases.GetGlobalCriticalOccupantAsync(excludingLeaseId, cancellationToken);

        return LeaseSchedulingRules.ValidateCapacity(
            globalCount,
            sessionCount,
            critical,
            risk,
            humanApprovedCritical,
            _options);
    }

    public void EnsureOwnership(ExecutionLease lease, LeaseOperationContext context)
    {
        var error = LeaseSchedulingRules.ValidateLeaseOwnership(
            lease, context.SessionId, context.Actor, context.RecoveryFlow);
        if (error is not null)
            throw LeaseOrchestrationException.Forbidden(error);
    }

    public void TouchHeartbeat(ExecutionLease lease, DateTimeOffset? now = null) =>
        lease.LastHeartbeatAt = now ?? DateTimeOffset.UtcNow;

    public async Task<ExecutionLease> RecordHeartbeatAsync(
        Guid cardId,
        LeaseOperationContext context,
        CancellationToken cancellationToken = default)
    {
        var lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound("No active lease for heartbeat.");

        EnsureOwnership(lease, context);
        TouchHeartbeat(lease);
        await _leases.UpdateAsync(lease, cancellationToken);
        return lease;
    }

    public async Task<ExecutionLease?> ExpireIfStaleAsync(
        ExecutionLease lease,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (!KanbanExecutionRules.IsActiveLease(lease))
            return null;

        var pastExpiry = LeaseSchedulerPolicy.IsPastExpiresAt(lease, now);
        var stale = LeaseSchedulerPolicy.IsHeartbeatStale(lease, now, _options);
        if (!pastExpiry && !stale)
            return null;

        var reason = LeaseSchedulerPolicy.DescribeStaleReason(lease, now, _options);
        var revoke = pastExpiry && LeaseSchedulerPolicy.ShouldRevokeOnExpiration(_options);
        return await ApplyExpirationAsync(lease, reason, revoke, cancellationToken);
    }

    public async Task<int> ProcessStaleActiveLeasesAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var active = await _leases.ListActiveAsync(cancellationToken);
        var count = 0;
        foreach (var lease in active)
        {
            if (await ExpireIfStaleAsync(lease, now, cancellationToken) is not null)
                count++;
        }

        return count;
    }

    public async Task<ExecutionLease> ApplyExpirationAsync(
        ExecutionLease lease,
        string reason,
        bool revoke,
        CancellationToken cancellationToken = default)
    {
        var from = lease.Status;
        var target = revoke ? ExecutionLeaseStatus.Revoked : ExecutionLeaseStatus.Blocked;
        var transitionError = KanbanExecutionRules.ValidateTransition(StatusChangeActor.System, from, target);
        if (transitionError is not null)
            throw LeaseOrchestrationException.Conflict(transitionError);

        lease.Status = target;
        lease.BlockedReason = reason;
        if (revoke)
            lease.RevokedAt = DateTimeOffset.UtcNow;

        lease.CriticalApprovalGranted = false;
        lease.CriticalApprovalConsumed = false;

        AppendEvidence(
            lease,
            revoke ? "lease.expired_revoked" : "lease.expired_blocked",
            StatusChangeActor.System,
            from,
            target,
            reason: reason,
            summary: reason,
            detail: new
            {
                lease.WorktreePath,
                lease.BranchName,
                lease.ExpiresAt,
                lease.LastHeartbeatAt,
            });

        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskAsync(lease.WorkTaskId, lease.Status, cancellationToken);

        await _events.IngestAsync(
            lease.WorkTaskId,
            EventSource.JoyZoning,
            EventTypes.ExecutionLeaseStatusChanged,
            new { lease.Id, from, target, reason, kind = "expiration" },
            cancellationToken);

        return lease;
    }

    public async Task<ReconciliationReport> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var report = new ReconciliationReport();
        var now = DateTimeOffset.UtcNow;

        report.StaleExpired = await ProcessStaleActiveLeasesAsync(cancellationToken);

        var active = await _leases.ListActiveAsync(cancellationToken);
        foreach (var lease in active)
        {
            if (!Directory.Exists(lease.WorktreePath))
            {
                await RepairInvalidAsync(
                    lease,
                    $"Worktree missing at {lease.WorktreePath}",
                    cancellationToken);
                report.MissingWorktree++;
                continue;
            }

            if (lease.Status == ExecutionLeaseStatus.Running && lease.ExecutionSessionId.HasValue)
            {
                var execution = await _executions.GetByIdAsync(lease.ExecutionSessionId.Value, cancellationToken);
                if (execution is not null && execution.Phase is ExecutionPhase.Completed
                    or ExecutionPhase.Failed
                    or ExecutionPhase.Cancelled
                    or ExecutionPhase.Interrupted)
                {
                    if (await TryRepairOrphanedViaHermesPollAsync(lease, execution, cancellationToken))
                    {
                        report.OrphanedRunning++;
                        continue;
                    }

                    await RepairOrphanedRunningAsync(lease, execution.Phase.ToString(), cancellationToken);
                    report.OrphanedRunning++;
                }
            }

            var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
            if (task?.Status == WorkTaskStatus.Complete && lease.Status != ExecutionLeaseStatus.Merged)
            {
                await RepairMergedTaskActiveLeaseAsync(lease, cancellationToken);
                report.MergedTaskActiveLease++;
            }

            if (lease.Status == ExecutionLeaseStatus.ReadyForReview
                && string.IsNullOrWhiteSpace(lease.VerificationReportJson))
            {
                await RepairInvalidAsync(lease, "ready_for_review without verification report", cancellationToken);
                report.InvalidReadyForReview++;
            }
        }

        return report;
    }

    private async Task<bool> TryRepairOrphanedViaHermesPollAsync(
        ExecutionLease lease,
        ExecutionSession execution,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(execution.HermesRunId))
            return false;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var agents = scope.ServiceProvider.GetRequiredService<AgentAdapterRegistry>();
            var consumer = scope.ServiceProvider.GetRequiredService<HermesRunEventConsumer>();

            var polled = await agents.Get(AgentKind.DietCode)
                .PollRunStatusAsync(execution.HermesRunId, cancellationToken);

            if (HermesRunEventRules.IsActiveRunStatus(polled?.Status))
            {
                consumer.ResumeTracking(execution.HermesRunId, AgentKind.DietCode, lease.WorkTaskId);
                return true;
            }

            if (polled?.Status is "completed")
            {
                if (execution.Phase != ExecutionPhase.Completed)
                    await _executions.UpdatePhaseAsync(execution.Id, ExecutionPhase.Completed, cancellationToken);

                var from = lease.Status;
                lease.Status = ExecutionLeaseStatus.Verifying;
                lease.BlockedReason = null;
                AppendEvidence(
                    lease,
                    "execution.completed",
                    StatusChangeActor.System,
                    from,
                    ExecutionLeaseStatus.Verifying,
                    summary: "Reconciliation: Hermes poll reported completed; lease moved to verifying.",
                    detail: new { execution.Id, execution.HermesRunId, polled.Status });

                await _leases.UpdateAsync(lease, cancellationToken);
                await SyncTaskAsync(lease.WorkTaskId, lease.Status, cancellationToken);
                return true;
            }
        }
        catch
        {
            // Hermes unavailable in unit tests or during outage — fall back to legacy repair.
        }

        return false;
    }

    private async Task RepairOrphanedRunningAsync(
        ExecutionLease lease,
        string phase,
        CancellationToken cancellationToken)
    {
        var from = lease.Status;

        if (phase == ExecutionPhase.Completed.ToString())
        {
            lease.Status = ExecutionLeaseStatus.Verifying;
            lease.BlockedReason = null;

            AppendEvidence(
                lease,
                "execution.completed",
                StatusChangeActor.System,
                from,
                ExecutionLeaseStatus.Verifying,
                summary: "Reconciliation: execution completed; lease moved to verifying.",
                detail: new { lease.ExecutionSessionId, phase });

            await _leases.UpdateAsync(lease, cancellationToken);
            await SyncTaskAsync(lease.WorkTaskId, lease.Status, cancellationToken);
            return;
        }

        lease.Status = ExecutionLeaseStatus.Blocked;
        lease.BlockedReason = $"Orphaned running lease; execution phase is {phase}.";

        AppendEvidence(
            lease,
            "execution.failed",
            StatusChangeActor.System,
            from,
            ExecutionLeaseStatus.Blocked,
            reason: lease.BlockedReason,
            summary: "Reconciliation: execution ended but lease was still running.",
            detail: new { lease.ExecutionSessionId, phase });

        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskAsync(lease.WorkTaskId, lease.Status, cancellationToken);
    }

    private async Task RepairInvalidAsync(
        ExecutionLease lease,
        string reason,
        CancellationToken cancellationToken)
    {
        var from = lease.Status;
        lease.Status = ExecutionLeaseStatus.Blocked;
        lease.BlockedReason = reason;

        AppendEvidence(
            lease,
            "reconciliation.repair",
            StatusChangeActor.System,
            from,
            ExecutionLeaseStatus.Blocked,
            reason: reason,
            summary: reason);

        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskAsync(lease.WorkTaskId, lease.Status, cancellationToken);
    }

    private async Task RepairMergedTaskActiveLeaseAsync(
        ExecutionLease lease,
        CancellationToken cancellationToken)
    {
        if (lease.Status == ExecutionLeaseStatus.ReadyForReview
            && !string.IsNullOrWhiteSpace(lease.VerificationReportJson))
        {
            var from = lease.Status;
            lease.Status = ExecutionLeaseStatus.Merged;
            lease.MergedAt = DateTimeOffset.UtcNow;
            AppendEvidence(lease, "reconciliation.merge", StatusChangeActor.System, from,
                ExecutionLeaseStatus.Merged, summary: "Task already complete; lease merged by reconciliation.");
            await _leases.UpdateAsync(lease, cancellationToken);
            return;
        }

        await RepairInvalidAsync(lease, "Task complete but lease still active", cancellationToken);
    }

    private async Task SyncTaskAsync(
        Guid cardId,
        ExecutionLeaseStatus leaseStatus,
        CancellationToken cancellationToken)
    {
        await _tasks.UpdateStatusAsync(
            cardId,
            KanbanExecutionRules.MapLeaseStatusToTaskStatus(leaseStatus),
            cancellationToken);
    }

    internal static void AppendEvidence(
        ExecutionLease lease,
        string kind,
        StatusChangeActor actor,
        ExecutionLeaseStatus? previous,
        ExecutionLeaseStatus? next,
        string? reason = null,
        string? summary = null,
        object? detail = null) =>
        LeaseEvidenceLog.Append(
            lease.EvidenceLogJson,
            v => lease.EvidenceLogJson = v,
            kind,
            actor,
            previous,
            next,
            reason,
            summary,
            detail);
}

public sealed class ReconciliationReport
{
    public int StaleExpired { get; set; }
    public int MissingWorktree { get; set; }
    public int OrphanedRunning { get; set; }
    public int MergedTaskActiveLease { get; set; }
    public int InvalidReadyForReview { get; set; }
}
