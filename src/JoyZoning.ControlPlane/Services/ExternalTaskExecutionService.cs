using System.Text.Json;
using JoyZoning.Adapters.Workspace;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

public sealed record ExternalTaskStartResult(
    WorkTask Task,
    OperatorSession Session,
    string BranchName,
    string WorkspacePath,
    string Prompt,
    string NextHumanAction);

public sealed record ExternalTaskStatusResponse(
    Guid TaskId,
    WorkTaskStatus Status,
    TaskExecutionMode TaskExecutionMode,
    ExecutionDriver ExecutionDriver,
    string? ExternalAgentName,
    string? BranchName,
    string? WorkspacePath,
    DateTimeOffset? StartedExternallyAt,
    DateTimeOffset? ReadyForReviewAt,
    DateTimeOffset? LastWorkspaceScanAt,
    ExternalVerificationStatus VerificationStatus,
    bool MergeRequired,
    bool ExternalMergeCompleted,
    string? BlockedReason,
    TaskWorkspaceScanResult? Workspace);

public sealed class ExternalTaskExecutionService
{
    private readonly IWorkTaskRepository _tasks;
    private readonly IOperatorSessionRepository _sessions;
    private readonly IExecutionLeaseRepository _leases;
    private readonly EventIngestor _events;
    private readonly IWorkspaceGitMerger _gitMerger;
    private readonly LeaseRuntimeOptions _options;

    public ExternalTaskExecutionService(
        IWorkTaskRepository tasks,
        IOperatorSessionRepository sessions,
        IExecutionLeaseRepository leases,
        EventIngestor events,
        IWorkspaceGitMerger gitMerger,
        IOptions<LeaseRuntimeOptions> options)
    {
        _tasks = tasks;
        _sessions = sessions;
        _leases = leases;
        _events = events;
        _gitMerger = gitMerger;
        _options = options.Value;
    }

    public async Task<ExternalTaskStartResult> StartExternalAsync(
        Guid taskId,
        string agent,
        CancellationToken cancellationToken = default)
    {
        if (!ExternalExecutionDriverMapping.TryParseAgent(agent, out var driver, out var displayName))
            throw ExternalTaskException.BadRequest("Agent is required (--agent cursor|claude-code|manual|...).");

        var task = await RequireTaskAsync(taskId, cancellationToken);
        var session = await RequireSessionAsync(task.OperatorSessionId, cancellationToken);

        var activeLease = await _leases.GetActiveByTaskIdAsync(taskId, cancellationToken);
        if (activeLease is not null)
            throw ExternalTaskException.Conflict("Task already has an active Hermes lease. Revoke it before external start.");

        if (task.Status is WorkTaskStatus.ExternalInProgress or WorkTaskStatus.ReadyForReview or WorkTaskStatus.Verified)
            throw ExternalTaskException.Conflict($"Task is already in external workflow ({task.Status}).");

        if (task.Status == WorkTaskStatus.Complete)
            throw ExternalTaskException.Conflict("Task is already complete.");

        await ValidateChainDispatchAsync(session, task, cancellationToken);

        if (!WorktreePlanner.TryPlan(session.WorkspaceRoot, taskId, out var workspacePath, out var branchName, out var planError))
            throw ExternalTaskException.BadRequest(planError!);

        var (branchOk, branchError) = await TaskGitWorkspace.EnsureBranchAsync(workspacePath, branchName, cancellationToken);
        if (!branchOk)
            throw ExternalTaskException.Conflict(branchError ?? "Failed to prepare task branch.");

        var allowedAreas = ExternalAgentPromptBuilder.DefaultAllowedAreas(task, session);
        var prompt = ExternalAgentPromptBuilder.Build(
            session.Name,
            task,
            session,
            workspacePath,
            branchName,
            allowedAreas);

        var now = DateTimeOffset.UtcNow;
        task.TaskExecutionMode = TaskExecutionMode.ExternalAgent;
        task.ExecutionDriver = driver;
        task.ExternalAgentName = displayName;
        task.BranchName = branchName;
        task.WorkspacePath = workspacePath;
        task.StartedExternallyAt = now;
        task.Status = WorkTaskStatus.ExternalInProgress;
        task.GeneratedPromptText = prompt;
        task.VerificationRequired = true;
        task.ExternalVerificationStatus = ExternalVerificationStatus.Pending;
        task.MergeRequired = true;
        task.ExternalMergeCompleted = false;
        task.KanbanRevision++;

        await ScanAndPersistWorkspaceAsync(task, session, cancellationToken);
        await _tasks.UpdateAsync(task, cancellationToken);

        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalWorkStarted,
            new { driver, displayName, branchName, workspacePath }, cancellationToken);
        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalAgentPromptGenerated,
            new { branchName, agent = displayName }, cancellationToken);

        return new ExternalTaskStartResult(
            task,
            session,
            branchName,
            workspacePath,
            prompt,
            "Edit in your external agent, then run: jz task mark-ready <task-id>");
    }

    public Task<string> GetPromptAsync(Guid taskId, CancellationToken cancellationToken = default) =>
        GetPromptInternalAsync(taskId, cancellationToken);

    public async Task<ExternalTaskStatusResponse> GetStatusAsync(
        Guid taskId,
        bool refreshWorkspace,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken);
        var session = await RequireSessionAsync(task.OperatorSessionId, cancellationToken);

        TaskWorkspaceScanResult? scan = null;
        if (refreshWorkspace && task.TaskExecutionMode == TaskExecutionMode.ExternalAgent)
        {
            scan = await ScanAndPersistWorkspaceAsync(task, session, cancellationToken);
            await _tasks.UpdateAsync(task, cancellationToken);
        }
        else if (task.LastWorkspaceScanAt.HasValue)
        {
            scan = BuildScanFromTask(task);
        }

        return MapStatus(task, scan);
    }

    public async Task<TaskWorkspaceScanResult> ScanWorkspaceAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken);
        var session = await RequireSessionAsync(task.OperatorSessionId, cancellationToken);
        if (task.TaskExecutionMode != TaskExecutionMode.ExternalAgent)
            throw ExternalTaskException.BadRequest("Task is not an external-agent task.");

        var scan = await ScanAndPersistWorkspaceAsync(task, session, cancellationToken);
        await _tasks.UpdateAsync(task, cancellationToken);

        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalWorkspaceStatusScanned,
            new { scan.CurrentBranch, scan.HasChanges, scan.BranchMatches }, cancellationToken);

        return scan;
    }

    public async Task<WorkTask> MarkReadyForReviewAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken);
        var session = await RequireSessionAsync(task.OperatorSessionId, cancellationToken);

        if (task.TaskExecutionMode != TaskExecutionMode.ExternalAgent)
            throw ExternalTaskException.BadRequest("Task is not an external-agent task.");

        if (task.Status != WorkTaskStatus.ExternalInProgress)
            throw ExternalTaskException.Conflict($"Mark ready requires status ExternalInProgress (current: {task.Status}).");

        var scan = await ScanAndPersistWorkspaceAsync(task, session, cancellationToken);

        if (!scan.BranchMatches)
            throw ExternalTaskException.Conflict(
                $"Branch mismatch: expected {task.BranchName}, current {scan.CurrentBranch ?? "(unknown)"}.");

        if (!scan.HasChanges)
            throw ExternalTaskException.Conflict("No workspace changes or commits to review.");

        task.Status = WorkTaskStatus.ReadyForReview;
        task.ReadyForReviewAt = DateTimeOffset.UtcNow;
        task.KanbanRevision++;
        await _tasks.UpdateAsync(task, cancellationToken);

        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalWorkMarkedReadyForReview,
            new { task.BranchName, changedFiles = scan.ChangedFiles.Count }, cancellationToken);

        return task;
    }

    public async Task<WorkTask> SubmitVerificationAsync(
        Guid taskId,
        VerificationReport report,
        CancellationToken cancellationToken = default)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken);

        if (task.TaskExecutionMode != TaskExecutionMode.ExternalAgent)
            throw ExternalTaskException.BadRequest("Task is not an external-agent task.");

        if (task.Status is not (WorkTaskStatus.ReadyForReview or WorkTaskStatus.Verified))
            throw ExternalTaskException.Conflict("Verify requires task status ReadyForReview.");

        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalVerificationStarted,
            new { commandCount = report.CommandsRun.Count }, cancellationToken);

        task.ExternalVerificationReportJson = JsonSerializer.Serialize(report);
        if (report.AllCommandsPassed)
        {
            task.ExternalVerificationStatus = ExternalVerificationStatus.Passed;
            task.Status = WorkTaskStatus.Verified;
            task.Verification = VerificationState.Passed;
            await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalVerificationPassed,
                new { report.ReadyForHumanReview }, cancellationToken);
        }
        else
        {
            task.ExternalVerificationStatus = ExternalVerificationStatus.Failed;
            task.Status = WorkTaskStatus.Blocked;
            task.Verification = VerificationState.Failed;
            await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalVerificationFailed,
                new { failed = report.CommandsRun.Count(r => !r.Passed) }, cancellationToken);
        }

        task.KanbanRevision++;
        await _tasks.UpdateAsync(task, cancellationToken);
        return task;
    }

    public async Task<AcceptResultResponse> CompleteExternalAsync(
        Guid taskId,
        bool operatorApproved,
        bool alreadyMerged,
        CancellationToken cancellationToken = default)
    {
        if (!operatorApproved)
            throw ExternalTaskException.Forbidden("Operator must explicitly approve complete (--yes).");

        var task = await RequireTaskAsync(taskId, cancellationToken);
        var session = await RequireSessionAsync(task.OperatorSessionId, cancellationToken);

        if (task.TaskExecutionMode != TaskExecutionMode.ExternalAgent)
            throw ExternalTaskException.BadRequest("Task is not an external-agent task.");

        if (task.Status is not (WorkTaskStatus.ReadyForReview or WorkTaskStatus.Verified))
            throw ExternalTaskException.Conflict(
                "Complete requires ReadyForReview or Verified. Mark ready and verify first.");

        if (task.VerificationRequired && task.ExternalVerificationStatus != ExternalVerificationStatus.Passed)
            throw ExternalTaskException.Conflict("Verification must pass before complete.");

        var activeLease = await _leases.GetActiveByTaskIdAsync(taskId, cancellationToken);
        if (activeLease is not null)
            throw ExternalTaskException.Conflict("Cannot complete external task while a Hermes lease is active.");

        WorkspaceConvergenceResult? convergence = null;
        if (!alreadyMerged && task.MergeRequired && !_options.MetadataOnlyAcceptResult)
        {
            var workspacePath = task.WorkspacePath ?? session.WorkspaceRoot;
            var (_, dirtyFiles) = await GitWorkspaceStatus.TryListChangedFilesAsync(workspacePath, cancellationToken);
            if (dirtyFiles.Count > 0)
                await TaskGitWorkspace.TryCommitAllAsync(workspacePath, "joyzoning-external-merge", cancellationToken);

            var request = new WorkspaceConvergenceRequest(
                session.WorkspaceRoot,
                workspacePath,
                task.BranchName ?? $"joyzoning/card-{task.Id:N}"[..20],
                DryRun: false,
                AllowDirtyDestination: _options.AllowDirtyDestination);

            convergence = await _gitMerger.ConvergeAsync(request, cancellationToken);
            if (!convergence.Succeeded)
                throw ExternalTaskException.Conflict(convergence.ErrorMessage ?? "Git merge failed.");
        }

        var now = DateTimeOffset.UtcNow;
        task.ExternalMergeCompleted = true;
        task.ExternalMergedAt = now;
        task.Status = WorkTaskStatus.Complete;
        task.CompletedAt = now;
        task.KanbanRevision++;
        await _tasks.UpdateAsync(task, cancellationToken);

        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalWorkMerged,
            new { alreadyMerged, strategy = convergence?.Strategy }, cancellationToken);
        await _events.IngestAsync(taskId, EventSource.JoyZoning, EventTypes.ExternalWorkCompleted,
            new { task.ExecutionDriver, task.ExternalAgentName }, cancellationToken);

        JsdpWorkspaceExecution.PruneLegacySandboxArtifacts(session.WorkspaceRoot);
        return new AcceptResultResponse(task, convergence, _options.MetadataOnlyAcceptResult);
    }

    public async Task ValidateTaskStatusChangeAsync(
        Guid taskId,
        WorkTaskStatus target,
        StatusChangeActor actor,
        CancellationToken cancellationToken = default)
    {
        if (target != WorkTaskStatus.Complete)
            return;

        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);
        if (task is null || task.TaskExecutionMode != TaskExecutionMode.ExternalAgent)
            return;

        if (task.ExternalMergeCompleted)
            return;

        throw ExternalTaskException.Forbidden(
            $"{JsdpMergeGate.ConvergenceRequiredCode}: external task requires review, verify, and merge via jz task complete --yes.");
    }

    private async Task ValidateChainDispatchAsync(
        OperatorSession session,
        WorkTask task,
        CancellationToken cancellationToken)
    {
        if (!session.DeliveryChainId.HasValue)
            return;

        var chainSessions = await _sessions.ListByDeliveryChainIdAsync(session.DeliveryChainId.Value, cancellationToken);
        var chainTasks = new List<WorkTask>();
        foreach (var chainSession in chainSessions)
            chainTasks.AddRange(await _tasks.ListBySessionAsync(chainSession.Id, cancellationToken));

        var chainLeases = new List<ExecutionLease>();
        foreach (var chainTask in chainTasks)
            chainLeases.AddRange(await _leases.ListByTaskIdAsync(chainTask.Id, cancellationToken));

        var error = RoleDeliveryChainGate.ValidateDispatch(
            session, task, chainSessions, chainTasks, chainLeases, _options);
        if (error is not null)
            throw ExternalTaskException.Conflict(error);
    }

    private async Task<string> GetPromptInternalAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await RequireTaskAsync(taskId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(task.GeneratedPromptText))
            return task.GeneratedPromptText;

        var session = await RequireSessionAsync(task.OperatorSessionId, cancellationToken);
        if (!WorktreePlanner.TryPlan(session.WorkspaceRoot, taskId, out var workspacePath, out var branchName, out _))
            throw ExternalTaskException.BadRequest("Cannot build prompt — invalid workspace.");

        return ExternalAgentPromptBuilder.Build(
            session.Name,
            task,
            session,
            workspacePath,
            branchName,
            ExternalAgentPromptBuilder.DefaultAllowedAreas(task, session));
    }

    private async Task<TaskWorkspaceScanResult> ScanAndPersistWorkspaceAsync(
        WorkTask task,
        OperatorSession session,
        CancellationToken cancellationToken)
    {
        var workspacePath = task.WorkspacePath ?? session.WorkspaceRoot;
        var currentBranch = await TaskGitWorkspace.GetCurrentBranchAsync(workspacePath, cancellationToken);
        var lastCommit = await TaskGitWorkspace.GetHeadCommitAsync(workspacePath, cancellationToken);
        var (_, changedFiles) = await GitWorkspaceStatus.TryListChangedFilesAsync(workspacePath, cancellationToken);
        var (staged, untracked) = TaskGitWorkspace.PartitionChangedFiles(changedFiles);
        var paths = changedFiles.Select(f => f.Path).ToList();

        var baseBranch = await TaskGitWorkspace.GetCurrentBranchAsync(session.WorkspaceRoot, cancellationToken);
        IReadOnlyList<string> diffAgainstBase = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(baseBranch))
        {
            diffAgainstBase = await TaskGitWorkspace.ListChangedFilesAgainstBaseAsync(
                workspacePath, $"origin/{baseBranch}", cancellationToken);
            if (diffAgainstBase.Count == 0)
                diffAgainstBase = await TaskGitWorkspace.ListChangedFilesAgainstBaseAsync(
                    workspacePath, "HEAD~1", cancellationToken);
        }

        var hasChanges = paths.Count > 0 || diffAgainstBase.Count > 0
            || !string.IsNullOrWhiteSpace(lastCommit) && lastCommit != task.LastObservedCommit;
        var branchMatches = string.Equals(currentBranch, task.BranchName, StringComparison.Ordinal);

        var scan = new TaskWorkspaceScanResult(
            task.Id,
            workspacePath,
            task.BranchName,
            currentBranch,
            branchMatches,
            hasChanges,
            paths.Count > 0 ? paths : diffAgainstBase,
            lastCommit,
            DateTimeOffset.UtcNow,
            paths.Count > 0,
            staged,
            untracked);

        task.LastWorkspaceScanAt = scan.ScannedAt;
        task.LastObservedCommit = lastCommit;
        task.HasUncommittedChanges = scan.HasUncommittedChanges;
        task.ChangedFilesJson = TaskGitWorkspace.SerializeChangedFiles(scan.ChangedFiles);

        return scan;
    }

    private static TaskWorkspaceScanResult BuildScanFromTask(WorkTask task) =>
        new(
            task.Id,
            task.WorkspacePath ?? string.Empty,
            task.BranchName,
            null,
            false,
            TaskGitWorkspace.DeserializeChangedFiles(task.ChangedFilesJson).Count > 0,
            TaskGitWorkspace.DeserializeChangedFiles(task.ChangedFilesJson),
            task.LastObservedCommit,
            task.LastWorkspaceScanAt ?? DateTimeOffset.UtcNow,
            task.HasUncommittedChanges,
            Array.Empty<string>(),
            Array.Empty<string>());

    private static ExternalTaskStatusResponse MapStatus(WorkTask task, TaskWorkspaceScanResult? scan)
    {
        string? blocked = null;
        if (task.Status == WorkTaskStatus.Blocked)
            blocked = "Verification failed.";
        else if (task.Status == WorkTaskStatus.ExternalInProgress && scan is { BranchMatches: false })
            blocked = $"Branch mismatch (expected {task.BranchName}).";

        return new ExternalTaskStatusResponse(
            task.Id,
            task.Status,
            task.TaskExecutionMode,
            task.ExecutionDriver,
            task.ExternalAgentName,
            task.BranchName,
            task.WorkspacePath,
            task.StartedExternallyAt,
            task.ReadyForReviewAt,
            task.LastWorkspaceScanAt,
            task.ExternalVerificationStatus,
            task.MergeRequired,
            task.ExternalMergeCompleted,
            blocked,
            scan);
    }

    private async Task<WorkTask> RequireTaskAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _tasks.GetByIdAsync(taskId, cancellationToken);
        return task ?? throw ExternalTaskException.NotFound($"Task {taskId} not found.");
    }

    private async Task<OperatorSession> RequireSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
        return session ?? throw ExternalTaskException.NotFound("Operator session not found.");
    }
}
