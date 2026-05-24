using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Evaluates authority policy and optionally auto-accepts ReadyForReview workers.</summary>
public sealed class AuthorityAutopilotService
{
    private readonly IWorkTaskRepository _tasks;
    private readonly IOperatorSessionRepository _sessions;
    private readonly IExecutionLeaseRepository _leases;
    private readonly AuthorityAutopilotMergeContextBuilder _mergeContext;
    private readonly AuthorityOptions _authority;
    private readonly LeaseRuntimeOptions _leaseRuntime;
    private readonly WorkspaceParallelismOptions _parallelism;
    private readonly ILogger<AuthorityAutopilotService> _logger;

    public AuthorityAutopilotService(
        IWorkTaskRepository tasks,
        IOperatorSessionRepository sessions,
        IExecutionLeaseRepository leases,
        AuthorityAutopilotMergeContextBuilder mergeContext,
        IOptions<AuthorityOptions> authority,
        IOptions<LeaseRuntimeOptions> leaseRuntime,
        IOptions<WorkspaceParallelismOptions> parallelism,
        ILogger<AuthorityAutopilotService> logger)
    {
        _tasks = tasks;
        _sessions = sessions;
        _leases = leases;
        _mergeContext = mergeContext;
        _authority = authority.Value;
        _leaseRuntime = leaseRuntime.Value;
        _parallelism = parallelism.Value;
        _logger = logger;
    }

    public AuthorityProfileKind ResolveProfile(OperatorSession session) =>
        AuthorityPolicyEvaluator.ResolveProfile(_authority, session.Id, session.Name);

    public AuthorityDecision EvaluateLease(
        ExecutionLease lease,
        WorkTask task,
        OperatorSession session,
        AutopilotMergeSnapshot merge)
    {
        var profile = ResolveProfile(session);
        var (verificationPassed, verificationMissing) = ResolveVerification(lease);
        var changedFiles = ResolveChangedFiles(lease, merge.Readiness, merge.OverlappingPaths);
        var changedFilesCount = Math.Max(merge.Readiness?.ChangedFilesCount ?? 0, changedFiles.Count);
        var unknownHead = string.IsNullOrWhiteSpace(merge.Readiness?.HeadCommit) && changedFiles.Count == 0;

        return AuthorityPolicyEvaluator.Evaluate(new AuthorityEvaluationInput(
            profile,
            _authority.AutopilotEnabled,
            _leaseRuntime.MetadataOnlyAcceptResult,
            lease.Status,
            task.Risk,
            merge.MergeState,
            verificationPassed,
            verificationMissing,
            HasConflicts(merge.MergeState, merge.Conflict),
            merge.OverlappingReadyWorker,
            merge.MergeObservabilityUnknown,
            DirtyWorktree: false,
            unknownHead,
            changedFilesCount,
            changedFiles,
            _parallelism.LargeChangeSetFileThreshold,
            _authority.Custom,
            session.ExecutionMode == SessionExecutionMode.BoundedRole));
    }

    public async Task<AuthorityAutopilotAttempt> TryAutoAcceptAsync(
        Guid cardId,
        Func<Task<AcceptResultResponse>> acceptResult,
        CancellationToken cancellationToken = default)
    {
        var lease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken);
        if (lease is null || lease.Status != ExecutionLeaseStatus.ReadyForReview)
            return AuthorityAutopilotAttempt.Skipped("Lease not ready for review.");

        var task = await _tasks.GetByIdAsync(cardId, cancellationToken);
        var session = task is null
            ? null
            : await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);

        if (task is null || session is null)
            return AuthorityAutopilotAttempt.Skipped("Task or session not found.");

        var merge = await _mergeContext.BuildForLeaseAsync(lease, session, cancellationToken);
        var decision = EvaluateLease(lease, task, session, merge);

        if (AuthorityEvidence.ShouldAppendDecision(
                lease.EvidenceLogJson, decision, merge.MergeState, merge.OverlappingPaths))
        {
            RecordDecisionEvidence(lease, decision, merge, attemptedAutoAccept: decision.AutoAcceptAllowed);
        }

        if (!decision.AutoAcceptAllowed)
        {
            if (AuthorityEvidence.ShouldAppendBlocked(
                    lease.EvidenceLogJson, decision, merge.MergeState, merge.OverlappingPaths))
            {
                RecordAutopilotBlockedEvidence(lease, decision, merge);
            }

            var blockedReason = FormatBlockedReason(decision, merge);
            if (!string.Equals(lease.BlockedReason, blockedReason, StringComparison.Ordinal))
            {
                lease.BlockedReason = blockedReason;
                await _leases.UpdateAsync(lease, cancellationToken);
            }

            return AuthorityAutopilotAttempt.Blocked(decision);
        }

        try
        {
            var accept = await acceptResult();
            var updatedLease = await _leases.GetActiveByTaskIdAsync(cardId, cancellationToken)
                ?? (await _leases.ListByTaskIdAsync(cardId, cancellationToken)).FirstOrDefault();

            if (updatedLease is not null)
            {
                LeaseRuntimeService.AppendEvidence(
                    updatedLease,
                    AuthorityEvidence.AutoAcceptedKind,
                    StatusChangeActor.System,
                    ExecutionLeaseStatus.ReadyForReview,
                    ExecutionLeaseStatus.Merged,
                    summary: "Autopilot auto-accepted worker into canonical workspace.",
                    detail: new
                    {
                        profile = decision.Profile.ToString(),
                        riskLevel = decision.RiskLevel.ToString(),
                        reasonCodes = decision.ReasonCodes,
                        autoAcceptedAt = DateTimeOffset.UtcNow,
                        gitConvergence = accept.GitConvergence is null
                            ? null
                            : GitConvergenceEvidence.ToEvidenceDetail(accept.GitConvergence),
                    });
                await _leases.UpdateAsync(updatedLease, cancellationToken);
            }

            return AuthorityAutopilotAttempt.Accepted(decision, accept);
        }
        catch (LeaseOrchestrationException ex)
        {
            lease.BlockedReason = ex.Message;
            LeaseRuntimeService.AppendEvidence(
                lease,
                AuthorityEvidence.AutoBlockedKind,
                StatusChangeActor.System,
                lease.Status,
                lease.Status,
                summary: "Autopilot accept failed at git convergence.",
                detail: new
                {
                    error = ex.Message,
                    merge.MergeState,
                    merge.MergeObservabilityUnknown,
                    overlappingPaths = merge.OverlappingPaths,
                    reasonCodes = decision.ReasonCodes,
                    humanMessages = decision.HumanMessages,
                });
            await _leases.UpdateAsync(lease, cancellationToken);
            return AuthorityAutopilotAttempt.ConvergenceFailed(decision, ex.Message);
        }
    }

    public async Task<AuthorityReconciliationReport> ReconcileReadyForReviewAsync(
        Func<Guid, CancellationToken, Task<AcceptResultResponse>> acceptForCard,
        Guid? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var report = new AuthorityReconciliationReport();
        if (!_authority.AutopilotEnabled || !_authority.ReconcileReadyForReview)
            return report;

        var active = await _leases.ListActiveAsync(cancellationToken);
        foreach (var lease in active)
        {
            if (lease.Status != ExecutionLeaseStatus.ReadyForReview)
                continue;
            if (string.IsNullOrWhiteSpace(lease.VerificationReportJson))
                continue;

            var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
            if (task is null)
                continue;
            if (sessionId is { } sid && task.OperatorSessionId != sid)
                continue;

            report.Evaluated++;
            var attempt = await TryAutoAcceptAsync(
                lease.WorkTaskId,
                () => acceptForCard(lease.WorkTaskId, cancellationToken),
                cancellationToken);

            switch (attempt.Outcome)
            {
                case "accepted":
                    report.AutoAccepted++;
                    break;
                case "blocked":
                    report.Blocked++;
                    break;
                case "convergence_failed":
                    report.ConvergenceFailed++;
                    break;
                default:
                    report.Skipped++;
                    break;
            }
        }

        return report;
    }

    private static string? FormatBlockedReason(AuthorityDecision decision, AutopilotMergeSnapshot merge)
    {
        if (merge.OverlappingReadyWorker && merge.OverlappingPaths.Count > 0)
            return $"Blocked: overlapping worker diff ({string.Join(", ", merge.OverlappingPaths.Take(5))}).";

        return decision.HumanMessages.FirstOrDefault();
    }

    private static void RecordDecisionEvidence(
        ExecutionLease lease,
        AuthorityDecision decision,
        AutopilotMergeSnapshot merge,
        bool attemptedAutoAccept)
    {
        LeaseRuntimeService.AppendEvidence(
            lease,
            AuthorityEvidence.DecisionKind,
            StatusChangeActor.System,
            lease.Status,
            lease.Status,
            summary: decision.AutoAcceptAllowed
                ? "Authority: auto-accept allowed."
                : "Authority: needs human review.",
            detail: AuthorityEvidence.ToEvidenceDetail(
                decision,
                attemptedAutoAccept,
                merge.MergeState,
                merge.OverlappingPaths));
    }

    private static void RecordAutopilotBlockedEvidence(
        ExecutionLease lease,
        AuthorityDecision decision,
        AutopilotMergeSnapshot merge)
    {
        LeaseRuntimeService.AppendEvidence(
            lease,
            AuthorityEvidence.AutoBlockedKind,
            StatusChangeActor.System,
            lease.Status,
            lease.Status,
            summary: decision.HumanMessages.FirstOrDefault() ?? "Autopilot blocked accept.",
            detail: AuthorityEvidence.ToBlockedDetail(
                decision,
                merge.MergeState,
                merge.OverlappingPaths,
                merge.MergeObservabilityUnknown));
    }

    private static (bool Passed, bool Missing) ResolveVerification(ExecutionLease lease)
    {
        if (string.IsNullOrWhiteSpace(lease.VerificationReportJson))
            return (false, true);

        try
        {
            var report = VerificationReportSerializer.Deserialize(lease.VerificationReportJson);
            return (VerificationReportValidator.IsPassingReport(report), false);
        }
        catch
        {
            return (false, true);
        }
    }

    private static List<string> ResolveChangedFiles(
        ExecutionLease lease,
        WorkerMergeReadiness? readiness,
        IReadOnlyList<string> overlappingPaths)
    {
        var files = new List<string>();

        if (!string.IsNullOrWhiteSpace(lease.VerificationReportJson))
        {
            try
            {
                var report = VerificationReportSerializer.Deserialize(lease.VerificationReportJson);
                files.AddRange(report.ChangedFiles.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            catch
            {
                // fall through
            }
        }

        if (files.Count == 0 && readiness?.ChangedFilesSummary is { Count: > 0 } summary)
            files.AddRange(summary);

        if (files.Count == 0 && overlappingPaths.Count > 0)
            files.AddRange(overlappingPaths);

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasConflicts(string mergeState, MergeConflictDetail? conflict) =>
        mergeState is "merge_conflict" or "merge_failed"
        || conflict is { ConflictFiles.Count: > 0 };
}

public sealed record AuthorityAutopilotAttempt(
    string Outcome,
    AuthorityDecision? Decision,
    AcceptResultResponse? AcceptResult,
    string? Message)
{
    public static AuthorityAutopilotAttempt Skipped(string message) =>
        new("skipped", null, null, message);

    public static AuthorityAutopilotAttempt Blocked(AuthorityDecision decision) =>
        new("blocked", decision, null, null);

    public static AuthorityAutopilotAttempt Accepted(AuthorityDecision decision, AcceptResultResponse accept) =>
        new("accepted", decision, accept, null);

    public static AuthorityAutopilotAttempt ConvergenceFailed(AuthorityDecision decision, string message) =>
        new("convergence_failed", decision, null, message);
}

public sealed class AuthorityReconciliationReport
{
    public int Evaluated { get; set; }
    public int AutoAccepted { get; set; }
    public int Blocked { get; set; }
    public int Skipped { get; set; }
    public int ConvergenceFailed { get; set; }
}
