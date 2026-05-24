using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;
using System.Text.Json;

// Planning → Execution → Review mode flow: kanban card (intent) → lease/worker (runtime) → merge queue (human PR).
// See docs/operational-modes.md — do not collapse these metaphors in UI or read models.

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Kanban card → lease → handoff → DietCode → verification → human merge.
/// Scheduling, heartbeat, recovery, and retries are enforced via <see cref="LeaseRuntimeService"/>.
/// </summary>
public class KanbanExecutionOrchestrator
{
    private readonly IWorkTaskRepository _tasks;
    private readonly IOperatorSessionRepository _sessions;
    private readonly IExecutionLeaseRepository _leases;
    private readonly EventIngestor _events;
    private readonly LeaseRuntimeService _runtime;
    private readonly LeaseRuntimeOptions _options;
    private readonly IWorkspaceGitMerger _gitMerger;
    private readonly AuthorityAutopilotService _autopilot;

    public KanbanExecutionOrchestrator(
        IWorkTaskRepository tasks,
        IOperatorSessionRepository sessions,
        IExecutionLeaseRepository leases,
        EventIngestor events,
        LeaseRuntimeService runtime,
        IOptions<LeaseRuntimeOptions> options,
        IWorkspaceGitMerger gitMerger,
        AuthorityAutopilotService autopilot)
    {
        _tasks = tasks;
        _sessions = sessions;
        _leases = leases;
        _events = events;
        _runtime = runtime;
        _options = options.Value;
        _gitMerger = gitMerger;
        _autopilot = autopilot;
    }

    public async Task<(ExecutionLease Lease, HandoffPacket Handoff)> BeginLeaseAsync(
        Guid cardId,
        bool humanApprovedCritical = false,
        LeaseOperationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(cardId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound($"Task {cardId} not found.");

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound("Operator session not found.");

        var op = context ?? new LeaseOperationContext(session.Id, StatusChangeActor.System);

        var active = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken);
        if (active is not null)
            throw LeaseOrchestrationException.Conflict("Card already has an active execution lease.");

        var leaseRisk = LeaseRiskMapper.FromTaskRisk(task.Risk);
        var schedulingError = await _runtime.ValidateSchedulingForNewLeaseAsync(
            session.Id, leaseRisk, humanApprovedCritical, cancellationToken: cancellationToken);
        if (schedulingError is not null)
            throw LeaseOrchestrationException.Conflict(schedulingError);

        if (!WorktreePlanner.TryPlan(
                session.WorkspaceRoot,
                cardId,
                task.HermesKanbanTaskId,
                session,
                out var worktreePath,
                out var branchName,
                out var pathError))
            throw LeaseOrchestrationException.BadRequest(pathError!);

        if (!JsdpWorkspaceExecution.IsCanonicalWorktree(session.WorkspaceRoot, worktreePath))
            throw LeaseOrchestrationException.Conflict(
                $"{JsdpSessionPolicy.EnforcedSessionCode}: JSDP lease must use canonical workspace, not an isolated sandbox.");

        var sessionTasks = await _tasks.ListBySessionAsync(session.Id, cancellationToken);
        var activeLeases = await _leases.ListActiveAsync(cancellationToken);
        var sessionActiveLeases = activeLeases
            .Where(l => l.OperatorSessionId == session.Id)
            .ToList();

        string? boundedError;
        if (JsdpSessionPolicy.RequiresEnforcement(session))
        {
            var integrityError = JsdpSessionPolicy.ValidateSessionIntegrity(session);
            if (integrityError is not null)
            {
                boundedError = integrityError;
            }
            else if (session.DeliveryChainId.HasValue)
            {
                var chainSessions = await _sessions.ListByDeliveryChainIdAsync(session.DeliveryChainId.Value, cancellationToken);
                var chainTasks = new List<WorkTask>();
                foreach (var chainSession in chainSessions)
                    chainTasks.AddRange(await _tasks.ListBySessionAsync(chainSession.Id, cancellationToken));

                var chainLeases = new List<ExecutionLease>();
                foreach (var chainTask in chainTasks)
                    chainLeases.AddRange(await _leases.ListByTaskIdAsync(chainTask.Id, cancellationToken));

                boundedError = RoleDeliveryChainGate.ValidateDispatch(
                    session, task, chainSessions, chainTasks, chainLeases, _options);
            }
            else
            {
                boundedError = JsdpSessionPolicy.ValidateSessionIntegrity(session);
            }
        }
        else
        {
            boundedError = BoundedSessionGate.ValidateDispatch(
                session, task, sessionTasks, sessionActiveLeases, _options);
        }

        if (boundedError is not null)
            throw LeaseOrchestrationException.Conflict(boundedError);

        var seedResult = new WorktreeSeeder.SeedResult(0, SkippedExistingContent: true);
        var handoff = HandoffPacketBuilder.Build(
            task, worktreePath, branchName, session.WorkspaceRoot, seedResult, session);
        var now = DateTimeOffset.UtcNow;
        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = cardId,
            OperatorSessionId = session.Id,
            AssignedSessionId = session.Id,
            AssignedAgent = task.AssignedAgent,
            CreatedBy = op.Actor,
            WorktreePath = worktreePath,
            BranchName = branchName,
            Status = ExecutionLeaseStatus.Leased,
            RiskLevel = leaseRisk,
            AllowedPathsJson = JsonSerializer.Serialize(handoff.AllowedPaths),
            ForbiddenPathsJson = JsonSerializer.Serialize(handoff.ForbiddenPaths),
            HandoffPacketJson = HandoffPacketBuilder.Serialize(handoff),
            EvidenceLogJson = "[]",
            StartedAt = now,
            LastHeartbeatAt = now,
            ExpiresAt = LeaseSchedulerPolicy.ComputeExpiresAt(leaseRisk, now, _options),
            CriticalApprovalGranted = leaseRisk == LeaseRiskLevel.Critical && humanApprovedCritical,
            CriticalApprovalConsumed = false,
            DispatchAttemptCount = 0,
        };

        AppendEvidence(lease, "lease.created", StatusChangeActor.System, null, ExecutionLeaseStatus.Leased,
            summary: "Execution lease created.",
            detail: new { lease.ExpiresAt, lease.AssignedSessionId });

        await _leases.CreateAsync(lease, cancellationToken);
        await ApplyTaskStatusAsync(task, KanbanExecutionRules.MapLeaseStatusToTaskStatus(lease.Status), cancellationToken);

        await _events.IngestAsync(cardId, EventSource.JoyZoning, EventTypes.ExecutionLeaseCreated,
            new { lease.Id, lease.WorktreePath, lease.BranchName, lease.RiskLevel }, cancellationToken);

        return (lease, handoff);
    }

    public Task<ExecutionLease> RecordHeartbeatAsync(
        Guid cardId,
        LeaseOperationContext context,
        CancellationToken cancellationToken = default) =>
        _runtime.RecordHeartbeatAsync(cardId, context, cancellationToken);

    public async Task<ExecutionLease> RecordAgentEvidenceAsync(
        Guid cardId,
        string kind,
        string summary,
        object? detail = null,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        AppendEvidence(lease, kind, StatusChangeActor.DietCode, lease.Status, lease.Status,
            summary: summary, detail: detail);
        _runtime.TouchHeartbeat(lease);
        await _leases.UpdateAsync(lease, cancellationToken);
        return lease;
    }

    public async Task<ExecutionLease> RecordDispatchAttemptAsync(
        Guid cardId,
        LeaseOperationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        if (context is not null)
            _runtime.EnsureOwnership(lease, context);

        if (lease.Status != ExecutionLeaseStatus.Leased)
            throw LeaseOrchestrationException.Conflict("Dispatch can only start from a leased card.");

        var criticalError = KanbanExecutionRules.ValidateCriticalDispatch(lease);
        if (criticalError is not null)
            throw LeaseOrchestrationException.Forbidden(criticalError);

        lease.DispatchAttemptCount++;
        var kind = lease.DispatchAttemptCount > 1 ? "dispatch.retry" : "dispatch.attempted";
        AppendEvidence(lease, kind, StatusChangeActor.System, lease.Status, lease.Status,
            summary: lease.DispatchAttemptCount > 1
                ? $"Dispatch retry #{lease.DispatchAttemptCount}."
                : "Hermes/DietCode dispatch started.",
            detail: new { lease.DispatchAttemptCount });

        _runtime.TouchHeartbeat(lease);
        await _leases.UpdateAsync(lease, cancellationToken);
        return lease;
    }

    public async Task<ExecutionLease> RecordDispatchRetryAsync(
        Guid cardId,
        bool humanApprovedCritical,
        LeaseOperationContext context,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        _runtime.EnsureOwnership(lease, context);

        var retryError = LeaseSchedulingRules.ValidateDispatchRetry(lease, humanApprovedCritical);
        if (retryError is not null)
            throw LeaseOrchestrationException.Conflict(retryError);

        if (lease.RiskLevel == LeaseRiskLevel.Critical && humanApprovedCritical)
        {
            lease.CriticalApprovalGranted = true;
            lease.CriticalApprovalConsumed = false;
        }

        return await RecordDispatchAttemptAsync(cardId, context, cancellationToken);
    }

    public async Task<ExecutionLease> MarkLeaseRunningAsync(
        Guid cardId,
        Guid executionSessionId,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        var from = lease.Status;

        var transitionError = KanbanExecutionRules.ValidateTransition(
            StatusChangeActor.System, from, ExecutionLeaseStatus.Running);
        if (transitionError is not null)
            throw LeaseOrchestrationException.Conflict(transitionError);

        if (lease.RiskLevel == LeaseRiskLevel.Critical)
            lease.CriticalApprovalConsumed = true;

        lease.ExecutionSessionId = executionSessionId;
        lease.Status = ExecutionLeaseStatus.Running;

        AppendEvidence(lease, "dispatch.succeeded", StatusChangeActor.System, from, ExecutionLeaseStatus.Running,
            summary: "Dispatch succeeded; lease is running.");

        _runtime.TouchHeartbeat(lease);
        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(cardId, lease.Status, cancellationToken);
        return lease;
    }

    public async Task<ExecutionLease> RecordDispatchFailureAsync(
        Guid cardId,
        string failureSummary,
        bool revoke = false,
        CancellationToken cancellationToken = default)
    {
        var lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound("No active lease to record dispatch failure.");

        if (lease.Status != ExecutionLeaseStatus.Leased)
            throw LeaseOrchestrationException.Conflict(
                "Dispatch failure can only be recorded for a leased card.");

        return await TransitionWithEvidenceAsync(
            lease,
            revoke ? ExecutionLeaseStatus.Revoked : ExecutionLeaseStatus.Blocked,
            "dispatch.failed",
            failureSummary,
            failureSummary,
            revoke ? (Action<ExecutionLease>)(l => l.RevokedAt = DateTimeOffset.UtcNow) : null,
            cancellationToken);
    }

    public async Task<ExecutionLease> RecordExecutionFailureAsync(
        Guid cardId,
        string failureSummary,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        if (lease.Status != ExecutionLeaseStatus.Running)
            throw LeaseOrchestrationException.Conflict("Execution failure applies to running leases.");

        return await TransitionWithEvidenceAsync(
            lease,
            ExecutionLeaseStatus.Blocked,
            "execution.failed",
            failureSummary,
            failureSummary,
            cancellationToken: cancellationToken);
    }

    public async Task<ExecutionLease> AgentTransitionLeaseAsync(
        Guid cardId,
        ExecutionLeaseStatus target,
        string? reason = null,
        LeaseOperationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        if (context is not null)
            _runtime.EnsureOwnership(lease, context);

        var from = lease.Status;
        var error = KanbanExecutionRules.ValidateTransition(StatusChangeActor.DietCode, from, target, reason);
        if (error is not null)
            throw LeaseOrchestrationException.Conflict(error);

        if (target == ExecutionLeaseStatus.Blocked)
            lease.BlockedReason = reason?.Trim();

        lease.Status = target;
        AppendEvidence(lease, "agent.status_changed", StatusChangeActor.DietCode, from, target,
            reason: reason,
            summary: $"Agent moved lease to {target}.");

        _runtime.TouchHeartbeat(lease);
        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(cardId, lease.Status, cancellationToken);

        await _events.IngestAsync(cardId, EventSource.DietCode, EventTypes.ExecutionLeaseStatusChanged,
            new { lease.Id, from, target, reason }, cancellationToken);

        return lease;
    }

    public async Task<ExecutionLease> SubmitVerificationAsync(
        Guid cardId,
        VerificationReport report,
        bool supersede = false,
        LeaseOperationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        if (context is not null)
            _runtime.EnsureOwnership(lease, context);

        var from = lease.Status;

        var replaceError = VerificationReportValidator.ValidateCanReplaceExistingPassingReport(
            lease.VerificationReportJson, report, supersede);
        if (replaceError is not null)
            throw LeaseOrchestrationException.Conflict(replaceError);

        var error = VerificationReportValidator.ValidatePassingSubmission(lease, report);
        if (error is not null)
            throw LeaseOrchestrationException.BadRequest(error);

        var task = await _tasks.GetByIdAsync(cardId, cancellationToken);
        if (task is not null)
        {
            var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
            if (session is not null)
            {
                var jsdpError = JsdpHandoffCompliance.ValidateVerificationReport(task, session, report);
                if (jsdpError is not null)
                    throw LeaseOrchestrationException.BadRequest(jsdpError);
            }
        }

        lease.VerificationReportJson = VerificationReportSerializer.Serialize(report);
        lease.Status = ExecutionLeaseStatus.ReadyForReview;

        AppendEvidence(lease,
            supersede ? "verification.superseded" : "verification.submitted",
            StatusChangeActor.DietCode,
            from,
            ExecutionLeaseStatus.ReadyForReview,
            summary: supersede
                ? "Verification report superseded and replaced."
                : "Verification passed; ready for human review.",
            detail: new { report.CommandsRun.Count, report.ReadyForHumanReview, supersede });

        _runtime.TouchHeartbeat(lease);
        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(cardId, lease.Status, cancellationToken);

        await _events.IngestAsync(cardId, EventSource.DietCode, EventTypes.VerificationReportAttached,
            new { lease.Id, passed = true }, cancellationToken);

        _ = await _autopilot.TryAutoAcceptAsync(
            cardId,
            () => AcceptResultAsync(cardId, StatusChangeActor.System, cancellationToken),
            cancellationToken);

        return await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken) ?? lease;
    }

    public async Task<ExecutionLease> SubmitFailedVerificationAsync(
        Guid cardId,
        VerificationReport report,
        LeaseOperationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var lease = await RequireActiveLeaseAsync(cardId, cancellationToken);
        if (context is not null)
            _runtime.EnsureOwnership(lease, context);

        var from = lease.Status;

        var structureError = VerificationReportValidator.ValidateStructure(report, lease.WorkTaskId);
        if (structureError is not null)
            throw LeaseOrchestrationException.BadRequest(structureError);

        if (lease.Status != ExecutionLeaseStatus.Verifying)
            throw LeaseOrchestrationException.Conflict(
                "Failed verification can only be recorded while lease is verifying.");

        if (report.AllCommandsPassed)
            throw LeaseOrchestrationException.BadRequest(
                "Use the passing verification endpoint when all commands pass.");

        lease.FailedVerificationReportJson = VerificationReportSerializer.Serialize(report);
        lease.Status = ExecutionLeaseStatus.Verifying;

        AppendEvidence(lease, "verification.failed", StatusChangeActor.DietCode, from, ExecutionLeaseStatus.Verifying,
            summary: "Verification failed; lease remains in verifying.",
            detail: new
            {
                failedCommands = report.CommandsRun.Where(c => !c.Passed).Select(c => c.Command).ToList(),
            });

        _runtime.TouchHeartbeat(lease);
        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(cardId, lease.Status, cancellationToken);

        return lease;
    }

    public async Task<ExecutionLease> RecoverLeaseAsync(
        Guid cardId,
        LeaseRecoveryMode mode,
        LeaseOperationContext context,
        bool humanApprovedCritical = false,
        CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(cardId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound($"Task {cardId} not found.");

        var lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken)
            ?? (await _leases.ListByTaskIdAsync(cardId, cancellationToken)).FirstOrDefault();

        var recoveryError = LeaseSchedulingRules.ValidateRecovery(lease, mode, context.Actor);
        if (recoveryError is not null)
            throw LeaseOrchestrationException.Conflict(recoveryError);

        context = context with { RecoveryFlow = true };

        return mode switch
        {
            LeaseRecoveryMode.ReopenBlocked => await ReopenBlockedAsync(task, lease!, context, humanApprovedCritical, cancellationToken),
            LeaseRecoveryMode.ReattachWorktree => await ReattachWorktreeAsync(task, lease!, context, humanApprovedCritical, cancellationToken),
            LeaseRecoveryMode.ReplacementLease => await ReplacementLeaseAsync(task, lease, context, humanApprovedCritical, cancellationToken),
            _ => throw LeaseOrchestrationException.BadRequest("Unknown recovery mode."),
        };
    }

    public async Task<ExecutionLease> RevokeLeaseAsync(
        Guid cardId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken)
            ?? (await _leases.ListByTaskIdAsync(cardId, cancellationToken)).FirstOrDefault();

        if (lease is null)
            throw LeaseOrchestrationException.NotFound("No lease found for card.");

        if (KanbanExecutionRules.IsTerminal(lease.Status))
            return lease;

        var task = await _tasks.GetByIdAsync(cardId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound($"Task {cardId} not found.");

        var error = KanbanExecutionRules.ValidateHumanRevoke(lease);
        if (error is not null)
            throw LeaseOrchestrationException.Conflict(error);

        var from = lease.Status;
        lease.Status = ExecutionLeaseStatus.Revoked;
        lease.RevokedAt = DateTimeOffset.UtcNow;

        AppendEvidence(lease, "lease.revoked", StatusChangeActor.Human, from, ExecutionLeaseStatus.Revoked,
            reason: reason,
            summary: reason ?? "Lease revoked by operator.",
            detail: new
            {
                preservedVerification = lease.VerificationReportJson is not null,
                preservedFailedVerification = lease.FailedVerificationReportJson is not null,
                lease.WorktreePath,
                lease.BranchName,
            });

        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(cardId, lease.Status, cancellationToken);

        await _events.IngestAsync(cardId, EventSource.JoyZoning, EventTypes.ExecutionLeaseRevoked,
            new { lease.Id, reason }, cancellationToken);

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is not null)
            JsdpWorkspaceExecution.PruneLegacySandboxArtifacts(session.WorkspaceRoot);

        return lease;
    }

    public Task<AcceptResultResponse> ApproveMergeAsync(
        Guid cardId,
        CancellationToken cancellationToken = default) =>
        AcceptResultAsync(cardId, StatusChangeActor.Human, cancellationToken);

    /// <summary>Accept ReadyForReview worker output into the canonical workspace, then mark Merged/Complete.</summary>
    public Task<AcceptResultResponse> AcceptResultAsync(
        Guid cardId,
        CancellationToken cancellationToken = default) =>
        AcceptResultAsync(cardId, StatusChangeActor.Human, cancellationToken);

    public async Task<AcceptResultResponse> AcceptResultAsync(
        Guid cardId,
        StatusChangeActor actor,
        CancellationToken cancellationToken = default)
    {
        var lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound("No active lease to accept.");

        var error = KanbanExecutionRules.ValidateHumanMerge(lease);
        if (error is not null)
            throw LeaseOrchestrationException.Conflict(error);

        var task = await _tasks.GetByIdAsync(cardId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound($"Task {cardId} not found.");

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound("Operator session not found.");

        if (JsdpSessionPolicy.RequiresEnforcement(session) && _options.MetadataOnlyAcceptResult)
        {
            throw LeaseOrchestrationException.Conflict(
                $"{JsdpSessionPolicy.EnforcedSessionCode}: metadata-only accept is disabled for JSDP bounded-role sessions. "
                + "Enable real git convergence for accept-merge.");
        }

        WorkspaceConvergenceResult? convergence = null;

        if (_options.MetadataOnlyAcceptResult)
        {
            if (string.IsNullOrWhiteSpace(lease.WorktreePath))
                throw LeaseOrchestrationException.Conflict("Worker worktree path is missing.");

            AppendEvidence(lease, "git.convergence.skipped", actor, lease.Status, lease.Status,
                summary: "Metadata-only accept (unsafe/dev). Git convergence was not run.",
                detail: new { metadataOnly = true });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(lease.WorktreePath))
                throw LeaseOrchestrationException.Conflict("Worker worktree path is missing.");

            if (string.IsNullOrWhiteSpace(session.WorkspaceRoot) || !Directory.Exists(session.WorkspaceRoot))
                throw LeaseOrchestrationException.Conflict("Canonical session workspace is missing.");

            if (JsdpWorkspaceExecution.IsCanonicalWorktree(session.WorkspaceRoot, lease.WorktreePath)
                && !string.IsNullOrWhiteSpace(lease.BranchName))
            {
                await TaskGitWorkspace.EnsureBranchAsync(lease.WorktreePath, lease.BranchName, cancellationToken);
            }

            var request = new WorkspaceConvergenceRequest(
                session.WorkspaceRoot,
                lease.WorktreePath,
                lease.BranchName,
                DryRun: false,
                AllowDirtyDestination: _options.AllowDirtyDestination);

            convergence = await _gitMerger.ConvergeAsync(request, cancellationToken);

            if (!convergence.Succeeded)
            {
                lease.BlockedReason = convergence.ErrorMessage;
                AppendEvidence(lease, GitConvergenceEvidence.FailedKind, actor,
                    lease.Status, lease.Status,
                    summary: "Git convergence failed; lease remains ready for review.",
                    detail: GitConvergenceEvidence.ToEvidenceDetail(convergence));

                await _leases.UpdateAsync(lease, cancellationToken);
                throw LeaseOrchestrationException.Conflict(
                    convergence.ErrorMessage ?? "Git convergence failed.");
            }

            AppendEvidence(lease, GitConvergenceEvidence.SucceededKind, actor,
                lease.Status, lease.Status,
                summary: "Worker changes applied to canonical workspace.",
                detail: GitConvergenceEvidence.ToEvidenceDetail(convergence));

            await _leases.UpdateAsync(lease, cancellationToken);

            lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken)
                ?? throw LeaseOrchestrationException.Conflict(
                    "Lease is no longer active after git convergence. Verify canonical workspace and lease history.");

            if (lease.Status != ExecutionLeaseStatus.ReadyForReview)
            {
                throw LeaseOrchestrationException.Conflict(
                    "Lease is no longer ReadyForReview (another accept may have completed). " +
                    "Check git.convergence.succeeded evidence and canonical workspace HEAD.");
            }
        }

        var from = lease.Status;
        lease.Status = ExecutionLeaseStatus.Merged;
        lease.MergedAt = DateTimeOffset.UtcNow;
        lease.BlockedReason = null;

        AppendEvidence(lease, "lease.merged", actor, from, ExecutionLeaseStatus.Merged,
            summary: _options.MetadataOnlyAcceptResult
                ? "Result accepted (metadata only)."
                : "Result accepted; code converged into canonical workspace.",
            detail: convergence is null ? null : GitConvergenceEvidence.ToEvidenceDetail(convergence));

        await _leases.UpdateAsync(lease, cancellationToken);
        await ApplyTaskStatusAsync(task, WorkTaskStatus.Complete, cancellationToken);

        await _events.IngestAsync(cardId, EventSource.JoyZoning, EventTypes.ExecutionLeaseMerged,
            new { lease.Id, gitConvergence = convergence?.Strategy }, cancellationToken);

        JsdpWorkspaceExecution.PruneLegacySandboxArtifacts(session.WorkspaceRoot);

        return new AcceptResultResponse(task, convergence, _options.MetadataOnlyAcceptResult);
    }

    public async Task ValidateTaskStatusChangeAsync(
        Guid cardId,
        WorkTaskStatus target,
        StatusChangeActor actor,
        CancellationToken cancellationToken = default)
    {
        if (actor == StatusChangeActor.DietCode)
        {
            var agentError = KanbanExecutionRules.ValidateAgentTaskStatusChange(target);
            if (agentError is not null)
                throw LeaseOrchestrationException.Forbidden(agentError);
        }

        if (target == WorkTaskStatus.Complete && actor != StatusChangeActor.Human)
            throw LeaseOrchestrationException.Forbidden("Only a human can mark a card done (merge).");

        if (target != WorkTaskStatus.Complete)
            return;

        var task = await _tasks.GetByIdAsync(cardId, cancellationToken);
        if (task is null || task.Status == WorkTaskStatus.Complete)
            return;

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is null || !JsdpSessionPolicy.RequiresEnforcement(session))
            return;

        var taskLeases = await _leases.ListByTaskIdAsync(cardId, cancellationToken);
        if (JsdpMergeGate.IsRoleConverged(task, taskLeases))
            return;

        throw LeaseOrchestrationException.Forbidden(
            $"{JsdpMergeGate.ConvergenceRequiredCode}: JSDP bounded role tasks require accept-merge before Complete. "
            + "Use `jz task complete <taskId>` (merge API), not direct status changes.");
    }

    public Task<ExecutionLease?> GetActiveLeaseAsync(Guid cardId, CancellationToken cancellationToken = default) =>
        _leases.GetActiveByTaskIdAsync(cardId, cancellationToken);

    private async Task<ExecutionLease> ReopenBlockedAsync(
        WorkTask task,
        ExecutionLease lease,
        LeaseOperationContext context,
        bool humanApprovedCritical,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is not null)
            JsdpWorkspaceExecution.TryAlignLeaseToCanonical(lease, session, task, out _);

        var from = lease.Status;
        var now = DateTimeOffset.UtcNow;
        lease.Status = ExecutionLeaseStatus.Leased;
        lease.BlockedReason = null;
        lease.LastHeartbeatAt = now;
        lease.ExpiresAt = LeaseSchedulerPolicy.ComputeExpiresAt(lease.RiskLevel, now, _options);
        ApplyCriticalReapproval(lease, humanApprovedCritical);

        AppendEvidence(lease, "recovery.reopen", context.Actor, from, ExecutionLeaseStatus.Leased,
            summary: "Blocked lease reopened for dispatch.",
            detail: new { lease.RecoveredFromLeaseId, lease.WorktreePath });

        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(task.Id, lease.Status, cancellationToken);
        return lease;
    }

    private async Task<ExecutionLease> ReattachWorktreeAsync(
        WorkTask task,
        ExecutionLease lease,
        LeaseOperationContext context,
        bool humanApprovedCritical,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is not null)
            JsdpWorkspaceExecution.TryAlignLeaseToCanonical(lease, session, task, out _);

        if (!Directory.Exists(lease.WorktreePath))
            throw LeaseOrchestrationException.Conflict("Worktree path is missing; use ReplacementLease or recreate worktree.");

        var from = lease.Status;
        var now = DateTimeOffset.UtcNow;
        lease.Status = ExecutionLeaseStatus.Leased;
        lease.BlockedReason = null;
        lease.LastHeartbeatAt = now;
        lease.ExpiresAt = LeaseSchedulerPolicy.ComputeExpiresAt(lease.RiskLevel, now, _options);
        lease.ExecutionSessionId = null;
        ApplyCriticalReapproval(lease, humanApprovedCritical);

        AppendEvidence(lease, "recovery.reattach", context.Actor, from, ExecutionLeaseStatus.Leased,
            summary: "Reattached to existing worktree.",
            detail: new { lease.WorktreePath, lease.BranchName });

        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(task.Id, lease.Status, cancellationToken);
        return lease;
    }

    private async Task<ExecutionLease> ReplacementLeaseAsync(
        WorkTask task,
        ExecutionLease? prior,
        LeaseOperationContext context,
        bool humanApprovedCritical,
        CancellationToken cancellationToken)
    {
        if (prior is not null && KanbanExecutionRules.IsActiveLease(prior))
        {
            var from = prior.Status;
            prior.Status = ExecutionLeaseStatus.Revoked;
            prior.RevokedAt = DateTimeOffset.UtcNow;
            AppendEvidence(prior, "recovery.superseded", context.Actor, from, ExecutionLeaseStatus.Revoked,
                summary: "Lease superseded by replacement.",
                detail: new { prior.WorktreePath });
            await _leases.UpdateAsync(prior, cancellationToken);
        }

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken)
            ?? throw LeaseOrchestrationException.NotFound("Operator session not found.");

        var worktreePath = prior?.WorktreePath ?? "";
        var branchName = prior?.BranchName ?? "";
        if (string.IsNullOrEmpty(worktreePath)
            || !WorktreePlanner.TryPlan(session.WorkspaceRoot, task.Id, task.HermesKanbanTaskId, session, out worktreePath, out branchName, out _))
        {
            if (!WorktreePlanner.TryPlan(session.WorkspaceRoot, task.Id, task.HermesKanbanTaskId, session, out worktreePath, out branchName, out var pathError))
                throw LeaseOrchestrationException.BadRequest(pathError!);
        }

        var (newLease, _) = await BeginLeaseAsync(
            task.Id,
            humanApprovedCritical,
            context with { RecoveryFlow = true },
            cancellationToken);

        newLease.WorktreePath = worktreePath;
        newLease.BranchName = branchName;
        newLease.RecoveredFromLeaseId = prior?.Id;
        AppendEvidence(newLease, "recovery.replacement", context.Actor, ExecutionLeaseStatus.Leased,
            ExecutionLeaseStatus.Leased,
            summary: "Replacement lease linked to prior worktree.",
            detail: new { priorId = prior?.Id, worktreePath });

        await _leases.UpdateAsync(newLease, cancellationToken);
        return newLease;
    }

    private static void ApplyCriticalReapproval(ExecutionLease lease, bool humanApprovedCritical)
    {
        if (lease.RiskLevel != LeaseRiskLevel.Critical)
            return;

        if (!humanApprovedCritical)
            throw LeaseOrchestrationException.Forbidden("Critical recovery requires humanApprovedCritical.");

        lease.CriticalApprovalGranted = true;
        lease.CriticalApprovalConsumed = false;
    }

    private async Task<ExecutionLease> TransitionWithEvidenceAsync(
        ExecutionLease lease,
        ExecutionLeaseStatus target,
        string evidenceKind,
        string reason,
        string summary,
        Action<ExecutionLease>? mutate = null,
        CancellationToken cancellationToken = default)
    {
        var from = lease.Status;
        var transitionError = KanbanExecutionRules.ValidateTransition(StatusChangeActor.System, from, target);
        if (transitionError is not null)
            throw LeaseOrchestrationException.Conflict(transitionError);

        lease.Status = target;
        lease.BlockedReason = target == ExecutionLeaseStatus.Blocked ? reason : lease.BlockedReason;
        mutate?.Invoke(lease);

        AppendEvidence(lease, evidenceKind, StatusChangeActor.System, from, target,
            reason: reason,
            summary: summary,
            detail: new { lease.WorktreePath, lease.BranchName });

        await _leases.UpdateAsync(lease, cancellationToken);
        await SyncTaskFromLeaseAsync(lease.WorkTaskId, lease.Status, cancellationToken);

        await _events.IngestAsync(lease.WorkTaskId, EventSource.JoyZoning, EventTypes.ExecutionLeaseStatusChanged,
            new { lease.Id, from, target, reason, evidenceKind }, cancellationToken);

        return lease;
    }

    private async Task<ExecutionLease> RequireActiveLeaseAsync(
        Guid cardId,
        CancellationToken cancellationToken)
    {
        var lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken);
        if (lease is null)
            throw LeaseOrchestrationException.NotFound("No active execution lease for this card.");

        var expired = await _runtime.ExpireIfStaleAsync(lease, DateTimeOffset.UtcNow, cancellationToken);
        if (expired is not null)
            throw LeaseOrchestrationException.Conflict(
                "Lease expired or became stale; recover or create a new lease.");

        return lease;
    }

    private async Task SyncTaskFromLeaseAsync(
        Guid cardId,
        ExecutionLeaseStatus leaseStatus,
        CancellationToken cancellationToken)
    {
        var task = await _tasks.GetByIdAsync(cardId, cancellationToken);
        if (task is null) return;
        await ApplyTaskStatusAsync(task, KanbanExecutionRules.MapLeaseStatusToTaskStatus(leaseStatus), cancellationToken);
    }

    private Task ApplyTaskStatusAsync(
        WorkTask task,
        WorkTaskStatus status,
        CancellationToken cancellationToken)
    {
        task.Status = status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        if (status == WorkTaskStatus.Complete)
            task.CompletedAt = DateTimeOffset.UtcNow;

        return _tasks.UpdateStatusAsync(task.Id, status, cancellationToken);
    }

    private static void AppendEvidence(
        ExecutionLease lease,
        string kind,
        StatusChangeActor actor,
        ExecutionLeaseStatus? previous,
        ExecutionLeaseStatus? next,
        string? reason = null,
        string? summary = null,
        object? detail = null) =>
        LeaseRuntimeService.AppendEvidence(
            lease, kind, actor, previous, next, reason, summary, detail);
}
