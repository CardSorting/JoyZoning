using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Mirrors lease worktrees to isolated <c>.joyzoning/live/</c> folders (or session root in legacy mode)
/// and maintains per-mirror <c>JOYZONING_LIVE.md</c> progress files.
/// </summary>
public sealed class WorkspaceLiveMirrorService
{
    private static readonly HashSet<string> ExcludedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".joyzoning", ".claude", ".vscode", ".expo", "dist", "build",
    };

    private readonly IOperatorSessionRepository _sessions;
    private readonly IWorkTaskRepository _tasks;
    private readonly IExecutionLeaseRepository _leases;
    private readonly WorkspaceOptions _workspace;
    private readonly WorkspaceParallelismOptions _parallelism;
    private readonly IExecutionRepository _executions;
    private readonly WorkspaceLiveMirrorRegistry _registry;
    private readonly WorkspaceLiveMirrorObservabilityService _observability;
    private readonly ILogger<WorkspaceLiveMirrorService> _logger;

    public WorkspaceLiveMirrorService(
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IExecutionLeaseRepository leases,
        IExecutionRepository executions,
        IOptions<WorkspaceOptions> workspace,
        IOptions<WorkspaceParallelismOptions> parallelism,
        WorkspaceLiveMirrorRegistry registry,
        WorkspaceLiveMirrorObservabilityService observability,
        ILogger<WorkspaceLiveMirrorService> logger)
    {
        _sessions = sessions;
        _tasks = tasks;
        _leases = leases;
        _executions = executions;
        _workspace = workspace.Value;
        _parallelism = parallelism.Value;
        _registry = registry;
        _observability = observability;
        _logger = logger;
    }

    public async Task<WorkspaceLiveSnapshot?> RefreshForLeaseAsync(
        ExecutionLease lease,
        CancellationToken cancellationToken = default)
    {
        if (!_workspace.MirrorToSessionRoot)
            return null;

        var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
        if (task is null)
            return null;

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.WorkspaceRoot))
            return null;

        if (string.IsNullOrWhiteSpace(lease.WorktreePath) || !Directory.Exists(lease.WorktreePath))
            return null;

        var sessionRoot = Path.GetFullPath(session.WorkspaceRoot);
        var worktree = Path.GetFullPath(lease.WorktreePath);
        var worktreesRoot = Path.GetFullPath(Path.Combine(sessionRoot, ".joyzoning", "worktrees"));

        if (!worktree.StartsWith(worktreesRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !worktree.Equals(worktreesRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Skipping workspace mirror: worktree {Worktree} is outside session sandbox {Root}",
                worktree,
                sessionRoot);
            return null;
        }

        var activeInWorkspace = await _leases.CountActiveForWorkspaceAsync(session.WorkspaceRoot, cancellationToken);
        if (!TryResolveMirrorTarget(lease, sessionRoot, activeInWorkspace, out var target, out var resolveError))
        {
            if (!string.IsNullOrEmpty(resolveError))
            {
                _logger.LogWarning("Live mirror skipped for lease {LeaseId}: {Reason}", lease.Id, resolveError);
                _registry.RecordOutcome(
                    lease.Id,
                    LiveMirrorHealthState.SkippedParallelSharedRootGuard,
                    resolveError);
            }

            return null;
        }

        if (_registry.HasCollision(target.MirrorRoot, lease.Id))
        {
            var occupant = _registry.GetOccupant(target.MirrorRoot);
            _logger.LogWarning(
                "Live mirror collision: lease {LeaseId} cannot write to {MirrorRoot} (another active execution owns it)",
                lease.Id,
                target.MirrorRoot);
            _registry.RecordOutcome(
                lease.Id,
                LiveMirrorHealthState.SkippedCollision,
                $"Mirror path held by lease {occupant?.LeaseId}.",
                target.MirrorRoot,
                occupant?.LeaseId);
            return null;
        }

        if (!_registry.TryAcquire(target.MirrorRoot, lease.Id, task.Id, target.MirrorKeyId))
        {
            var occupant = _registry.GetOccupant(target.MirrorRoot);
            _logger.LogWarning(
                "Live mirror refused: lease {LeaseId} could not acquire {MirrorRoot}",
                lease.Id,
                target.MirrorRoot);
            _registry.RecordOutcome(
                lease.Id,
                LiveMirrorHealthState.SkippedCollision,
                "Could not acquire mirror path.",
                target.MirrorRoot,
                occupant?.LeaseId);
            return null;
        }

        try
        {
            Directory.CreateDirectory(target.MirrorRoot);
            var filesCopied = MirrorDirectory(worktree, target.MirrorRoot);
            var snapshot = await BuildSnapshotAsync(task, lease, target, worktree, filesCopied, cancellationToken);
            WriteMirrorMeta(target.MirrorRoot, snapshot, task, status: "active", completedAt: null);
            WriteLiveFile(target.MirrorRoot, snapshot, target);
            WriteLiveJson(target.MirrorRoot, snapshot);
            _registry.RecordOutcome(
                lease.Id,
                LiveMirrorHealthState.Active,
                detail: null,
                target.MirrorRoot);
            await _observability.WriteIndexFileAsync(session, activeInWorkspace, cancellationToken);
            PruneCompletedMirrors(sessionRoot, session);
            return snapshot;
        }
        catch (Exception ex)
        {
            _registry.RecordOutcome(
                lease.Id,
                LiveMirrorHealthState.FailedCopy,
                ex.Message,
                target.MirrorRoot);
            _registry.Release(target.MirrorRoot, lease.Id);
            throw;
        }
    }

    public async Task MarkMirrorStaleForExecutionEndAsync(
        ExecutionLease lease,
        CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
        if (task is null)
            return;

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.WorkspaceRoot))
            return;

        var sessionRoot = Path.GetFullPath(session.WorkspaceRoot);
        var activeInWorkspace = await _leases.CountActiveForWorkspaceAsync(session.WorkspaceRoot, cancellationToken);
        if (!TryResolveMirrorTarget(lease, sessionRoot, activeInWorkspace, out var target, out _))
            return;

        if (!Directory.Exists(target.MirrorRoot))
            return;

        var snapshot = await BuildSnapshotAsync(task, lease, target, lease.WorktreePath, filesCopied: 0, cancellationToken);
        WriteMirrorMeta(target.MirrorRoot, snapshot, task, status: "stale", completedAt: DateTimeOffset.UtcNow);
        _registry.RecordOutcome(lease.Id, LiveMirrorHealthState.Stale, mirrorRoot: target.MirrorRoot);
        await _observability.WriteIndexFileAsync(session, activeInWorkspace, cancellationToken);
    }

    public async Task CompleteMirrorForLeaseAsync(
        ExecutionLease lease,
        WorkerMergeState? terminalMergeState = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
        if (task is null)
            return;

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.WorkspaceRoot))
            return;

        var sessionRoot = Path.GetFullPath(session.WorkspaceRoot);
        var activeInWorkspace = await _leases.CountActiveForWorkspaceAsync(session.WorkspaceRoot, cancellationToken);

        if (!TryResolveMirrorTarget(lease, sessionRoot, activeInWorkspace, out var target, out _))
        {
            _registry.ReleaseForLease(lease.Id, sessionRoot);
            return;
        }

        _registry.Release(target.MirrorRoot, lease.Id);

        if (Directory.Exists(target.MirrorRoot))
        {
            var snapshot = await BuildSnapshotAsync(
                task,
                lease,
                target,
                lease.WorktreePath,
                filesCopied: 0,
                cancellationToken);
            var mergeState = terminalMergeState ?? lease.Status switch
            {
                ExecutionLeaseStatus.Revoked => WorkerMergeState.Revoked,
                ExecutionLeaseStatus.Merged => WorkerMergeState.Merged,
                _ => (WorkerMergeState?)null,
            };
            WriteMirrorMeta(
                target.MirrorRoot,
                snapshot,
                task,
                status: "completed",
                completedAt: DateTimeOffset.UtcNow,
                mergeState: mergeState);
            _registry.RecordOutcome(lease.Id, LiveMirrorHealthState.Completed, mirrorRoot: target.MirrorRoot);
        }

        await _observability.WriteIndexFileAsync(session, activeInWorkspace, cancellationToken);
        PruneCompletedMirrors(sessionRoot, session);
    }

    public async Task RecordMergeConflictForLeaseAsync(
        ExecutionLease lease,
        string category,
        string reason,
        IReadOnlyList<string>? conflictFiles = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _tasks.GetByIdAsync(lease.WorkTaskId, cancellationToken);
        if (task is null)
            return;

        var session = await _sessions.GetByIdAsync(task.OperatorSessionId, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.WorkspaceRoot))
            return;

        var sessionRoot = Path.GetFullPath(session.WorkspaceRoot);
        var activeInWorkspace = await _leases.CountActiveForWorkspaceAsync(session.WorkspaceRoot, cancellationToken);

        if (!TryResolveMirrorTarget(lease, sessionRoot, activeInWorkspace, out var target, out _))
            return;

        if (!Directory.Exists(target.MirrorRoot))
            return;

        var snapshot = await BuildSnapshotAsync(
            task,
            lease,
            target,
            lease.WorktreePath,
            filesCopied: 0,
            cancellationToken);
        WriteMirrorMeta(
            target.MirrorRoot,
            snapshot,
            task,
            status: "active",
            completedAt: null,
            mergeState: WorkerMergeState.MergeConflict,
            conflictCategory: category,
            conflictReason: reason,
            conflictFiles: conflictFiles);

        await _observability.WriteIndexFileAsync(session, activeInWorkspace, cancellationToken);
    }

    private bool TryResolveMirrorTarget(
        ExecutionLease lease,
        string sessionRoot,
        int activeLeasesInWorkspace,
        out MirrorTarget target,
        out string? error)
    {
        if (WorkspaceLiveMirrorTargetResolver.TryResolve(
                lease,
                sessionRoot,
                _workspace.MirrorToSessionRoot,
                _parallelism,
                activeLeasesInWorkspace,
                out var resolved,
                out error))
        {
            target = new MirrorTarget(
                resolved.MirrorRoot,
                resolved.SessionRoot,
                resolved.IsSharedSessionRoot,
                resolved.MirrorKeyId,
                resolved.Mode);
            return true;
        }

        target = default!;
        return false;
    }

    private void PruneCompletedMirrors(string sessionRoot, OperatorSession session)
    {
        var retentionDays = _parallelism.LiveMirrorRetentionDays;
        if (retentionDays <= 0)
            return;

        var liveBase = Path.Combine(sessionRoot, WorkspaceLiveMirrorPaths.LiveRootSegment);
        if (!Directory.Exists(liveBase))
            return;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        foreach (var taskDir in Directory.EnumerateDirectories(liveBase))
        {
            PruneMirrorDirectory(taskDir, cutoff);
            foreach (var execDir in Directory.EnumerateDirectories(taskDir))
                PruneMirrorDirectory(execDir, cutoff, session);
        }
    }

    private void PruneMirrorDirectory(string mirrorDir, DateTimeOffset cutoff, OperatorSession? session = null)
    {
        var metaPath = Path.Combine(mirrorDir, ".joyzoning", "mirror-meta.json");
        if (!File.Exists(metaPath))
            return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
            var status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
            if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                return;

            var mergeState = doc.RootElement.TryGetProperty("mergeState", out var ms)
                ? ms.GetString()
                : null;
            if (WorkerMergeStateResolver.BlocksPrune(WorkerMergeStateNames.Parse(mergeState)))
                return;

            if (!doc.RootElement.TryGetProperty("completedAt", out var atEl)
                || !DateTimeOffset.TryParse(atEl.GetString(), out var completedAt)
                || completedAt > cutoff)
                return;

            TryMarkPruned(mirrorDir);
            if (TryReadLeaseIdFromMeta(mirrorDir, out var leaseId))
                _registry.RecordOutcome(leaseId, LiveMirrorHealthState.Pruned, mirrorRoot: mirrorDir);

            Directory.Delete(mirrorDir, recursive: true);
            _logger.LogDebug("Pruned completed live mirror {MirrorDir}", mirrorDir);

            if (session is not null)
                _ = _observability.WriteIndexFileAsync(session, activeLeasesInWorkspace: 0);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to prune live mirror {MirrorDir}", mirrorDir);
        }
    }

    private static void TryMarkPruned(string mirrorDir)
    {
        var metaPath = Path.Combine(mirrorDir, ".joyzoning", "mirror-meta.json");
        if (!File.Exists(metaPath))
            return;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
            var body = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                body[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetInt64(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.ToString(),
                };
            body["status"] = "pruned";
            body["healthState"] = "pruned";
            body["completedAt"] = DateTimeOffset.UtcNow.ToString("O");
            File.WriteAllText(metaPath, JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort
        }
    }

    private static bool TryReadLeaseIdFromMeta(string mirrorDir, out Guid leaseId)
    {
        leaseId = Guid.Empty;
        var metaPath = Path.Combine(mirrorDir, ".joyzoning", "mirror-meta.json");
        if (!File.Exists(metaPath))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
            return doc.RootElement.TryGetProperty("leaseId", out var li)
                && Guid.TryParse(li.GetString(), out leaseId);
        }
        catch
        {
            return false;
        }
    }

    private void WriteMirrorMeta(
        string mirrorRoot,
        WorkspaceLiveSnapshot snapshot,
        WorkTask task,
        string status,
        DateTimeOffset? completedAt,
        WorkerMergeState? mergeState = null,
        string? conflictCategory = null,
        string? conflictReason = null,
        IReadOnlyList<string>? conflictFiles = null)
    {
        var metaDir = Path.Combine(mirrorRoot, ".joyzoning");
        Directory.CreateDirectory(metaDir);
        var body = new
        {
            status,
            healthState = status,
            mergeState = mergeState is null ? null : WorkerMergeStateNames.ToApiString(mergeState.Value),
            mergeConflict = conflictCategory is null
                ? null
                : new
                {
                    category = conflictCategory,
                    reason = conflictReason,
                    conflictFiles = conflictFiles ?? Array.Empty<string>(),
                },
            taskId = snapshot.TaskId,
            taskTitle = task.Title,
            leaseId = snapshot.LeaseId,
            executionSessionId = snapshot.MirrorKeyId,
            hermesSessionId = snapshot.HermesSessionId,
            mirrorRoot = snapshot.LiveMirrorRoot,
            mirrorMode = snapshot.MirrorMode,
            worktreePath = snapshot.WorktreePath,
            leaseStatus = snapshot.LeaseStatus,
            kanbanStatus = task.Status.ToString(),
            kanbanRevision = task.KanbanRevision,
            kanbanPushedRevision = task.KanbanPushedRevision,
            lastMirroredAt = snapshot.UpdatedAt,
            updatedAt = snapshot.UpdatedAt,
            completedAt,
        };
        File.WriteAllText(
            Path.Combine(metaDir, "mirror-meta.json"),
            JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
    }

    private int MirrorDirectory(string sourceRoot, string destRoot)
    {
        var copied = 0;
        foreach (var dir in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, dir);
            if (ShouldSkipRelativePath(relative))
                continue;

            var targetDir = Path.Combine(destRoot, relative);
            Directory.CreateDirectory(targetDir);
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            if (ShouldSkipRelativePath(relative))
                continue;

            var targetFile = Path.Combine(destRoot, relative);
            var targetParent = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(targetParent))
                Directory.CreateDirectory(targetParent);

            try
            {
                var srcInfo = new FileInfo(file);
                var dstInfo = new FileInfo(targetFile);
                if (dstInfo.Exists && dstInfo.LastWriteTimeUtc >= srcInfo.LastWriteTimeUtc
                    && dstInfo.Length == srcInfo.Length)
                    continue;

                File.Copy(file, targetFile, overwrite: true);
                copied++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Mirror skip {File}", relative);
            }
        }

        return copied;
    }

    private static bool ShouldSkipRelativePath(string relative)
    {
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => ExcludedDirNames.Contains(p));
    }

    private async Task<WorkspaceLiveSnapshot> BuildSnapshotAsync(
        WorkTask task,
        ExecutionLease lease,
        MirrorTarget target,
        string worktree,
        int filesCopied,
        CancellationToken cancellationToken)
    {
        string? hermesSessionId = null;
        if (lease.ExecutionSessionId is { } execId && execId != Guid.Empty)
        {
            var execution = await _executions.GetByIdAsync(execId, cancellationToken);
            hermesSessionId = execution?.HermesSessionId;
        }

        var counts = CountProjectFiles(target.MirrorRoot);
        var worktreeActivity = AnalyzeWorktreeActivity(worktree);
        var (recent, lastKind) = ParseRecentEvidence(lease.EvidenceLogJson, max: 8);
        var status = lease.Status.ToString();
        var presentation = WorkspaceLivePresentation.Build(
            task.Title,
            status,
            lease.BlockedReason,
            lease.EvidenceLogJson,
            counts.AppScreens,
            counts.FeatureFiles,
            counts.SharedFiles,
            counts.HasPackageJson,
            counts.HasReadme,
            worktreeActivity.FileCount,
            worktreeActivity.NewestWriteUtc,
            filesCopied);

        return new WorkspaceLiveSnapshot(
            UpdatedAt: DateTimeOffset.UtcNow,
            TaskId: task.Id,
            LeaseId: lease.Id,
            MirrorKeyId: target.MirrorKeyId,
            HermesSessionId: hermesSessionId,
            TaskTitle: task.Title,
            LeaseStatus: status,
            BlockedReason: lease.BlockedReason,
            EvidenceLogJson: lease.EvidenceLogJson,
            SessionWorkspaceRoot: target.SessionRoot,
            LiveMirrorRoot: target.MirrorRoot,
            MirrorMode: target.Mode.ToString(),
            IsSharedSessionRootMirror: target.IsSharedSessionRoot,
            WorktreePath: worktree,
            FilesCopiedThisTick: filesCopied,
            AppScreenCount: counts.AppScreens,
            FeatureFileCount: counts.FeatureFiles,
            SharedFileCount: counts.SharedFiles,
            HasPackageJson: counts.HasPackageJson,
            HasReadme: counts.HasReadme,
            WorktreeFileCount: worktreeActivity.FileCount,
            WorktreeLastWriteUtc: worktreeActivity.NewestWriteUtc,
            LastEvidenceKind: lastKind,
            RecommendedPollSeconds: presentation.RecommendedPollSeconds,
            RecentEvidence: recent,
            LiveFilePath: Path.Combine(target.MirrorRoot, _workspace.LiveStatusFileName),
            Presentation: presentation);
    }

    private void WriteLiveFile(string mirrorRoot, WorkspaceLiveSnapshot snapshot, MirrorTarget target)
    {
        var path = Path.Combine(mirrorRoot, _workspace.LiveStatusFileName);
        var d = snapshot.Presentation;
        var sb = new StringBuilder();

        sb.AppendLine($"# {d.Headline}");
        sb.AppendLine();
        sb.AppendLine($"**Right now:** {d.Subheadline}");
        sb.AppendLine();
        sb.AppendLine($"**{d.NavigationSummary}** ({d.PhaseLabel})");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(d.TimeGuidance))
            sb.AppendLine($"_{d.TimeGuidance}_");
        if (!string.IsNullOrWhiteSpace(d.StaleWarning))
            sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(d.StaleWarning))
            sb.AppendLine($"> ⚠️ {d.StaleWarning}");
        sb.AppendLine();
        sb.AppendLine($"*Last updated: {snapshot.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC*");
        sb.AppendLine();

        sb.AppendLine("## Overall progress");
        sb.AppendLine();
        sb.AppendLine($"**{d.ProgressPercent}%** complete");
        sb.AppendLine();
        sb.AppendLine(RenderMarkdownBar(d.ProgressPercent));
        sb.AppendLine();

        sb.AppendLine("## Build steps");
        sb.AppendLine();
        foreach (var step in d.Steps)
        {
            var icon = step.State switch
            {
                WorkspaceLivePresentation.StepComplete => "✅",
                WorkspaceLivePresentation.StepCurrent => "▶️",
                WorkspaceLivePresentation.StepFailed => "❌",
                _ => "⬜",
            };
            var suffix = step.State == WorkspaceLivePresentation.StepCurrent ? " **← current**" : "";
            sb.AppendLine($"- {icon} {step.Label}{suffix}");
        }

        sb.AppendLine();
        sb.AppendLine("## What's in your project folder");
        sb.AppendLine();
        foreach (var item in d.Deliverables)
        {
            var icon = item.Done ? "✅" : "⬜";
            var count = item.Count is > 0 ? $" ({item.Count})" : "";
            sb.AppendLine($"- {icon} {item.Label}{count}");
        }

        if (snapshot.FilesCopiedThisTick > 0)
            sb.AppendLine($"\n*{snapshot.FilesCopiedThisTick} file(s) synced to this live mirror this update.*");

        if (d.NextActions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## What you can do");
            sb.AppendLine();
            foreach (var action in d.NextActions)
                sb.AppendLine($"- {action}");
        }

        if (d.RecentActivity.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Recent activity");
            sb.AppendLine();
            foreach (var line in d.RecentActivity)
                sb.AppendLine($"- {line}");
        }

        if (d.HelpTips.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Good to know");
            sb.AppendLine();
            foreach (var tip in d.HelpTips)
                sb.AppendLine($"- {tip}");
        }

        sb.AppendLine();
        sb.AppendLine("<details>");
        sb.AppendLine("<summary>Technical details (for developers)</summary>");
        sb.AppendLine();
        sb.AppendLine($"- **Task:** {snapshot.TaskTitle}");
        sb.AppendLine($"- **Task ID:** `{snapshot.TaskId}`");
        sb.AppendLine($"- **Lease ID:** `{snapshot.LeaseId}`");
        sb.AppendLine($"- **Mirror key:** `{snapshot.MirrorKeyId}`");
        sb.AppendLine($"- **Status:** `{snapshot.LeaseStatus}`");
        if (!string.IsNullOrWhiteSpace(snapshot.BlockedReason))
            sb.AppendLine($"- **Blocked:** {snapshot.BlockedReason}");
        sb.AppendLine($"- **Canonical build folder:** `{snapshot.WorktreePath}`");
        sb.AppendLine($"- **Live mirror ({snapshot.MirrorMode}):** `{snapshot.LiveMirrorRoot}`");
        sb.AppendLine($"- **Session workspace:** `{snapshot.SessionWorkspaceRoot}`");
        if (target.IsSharedSessionRoot)
            sb.AppendLine("- **Note:** Files are mirrored into the session root (single-worker mode).");
        else
            sb.AppendLine("- **Note:** Parallel workers each have their own folder under `.joyzoning/live/` — see `index.json`.");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("jz task lease " + snapshot.TaskId);
        sb.AppendLine("open \"" + snapshot.LiveMirrorRoot + "\"");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("</details>");
        sb.AppendLine();
        sb.AppendLine("_Auto-generated by JoyZoning. This file is overwritten while the task runs._");

        File.WriteAllText(path, sb.ToString());
    }

    private void WriteLiveJson(string mirrorRoot, WorkspaceLiveSnapshot snapshot)
    {
        if (!_workspace.WriteLiveJsonFile)
            return;

        var jsonPath = Path.Combine(mirrorRoot, _workspace.LiveJsonRelativePath);
        var parent = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var d = snapshot.Presentation;
        var body = new
        {
            updatedAt = snapshot.UpdatedAt,
            taskId = snapshot.TaskId,
            leaseId = snapshot.LeaseId,
            mirrorKeyId = snapshot.MirrorKeyId,
            title = snapshot.TaskTitle,
            leaseStatus = snapshot.LeaseStatus,
            liveMarkdown = snapshot.LiveFilePath,
            liveMirrorRoot = snapshot.LiveMirrorRoot,
            mirrorMode = snapshot.MirrorMode,
            isSharedSessionRootMirror = snapshot.IsSharedSessionRootMirror,
            sessionWorkspaceRoot = snapshot.SessionWorkspaceRoot,
            recommendedPollSeconds = d.RecommendedPollSeconds,
            pollMode = d.PollMode,
            display = new
            {
                d.Headline,
                d.Subheadline,
                d.ActivityState,
                d.ProgressPercent,
                d.StepProgressLabel,
                d.PhaseLabel,
                d.NavigationSummary,
                d.CurrentStepTitle,
                d.TimeGuidance,
                d.StaleWarning,
                steps = d.Steps.Select(s => new { s.Id, s.Label, s.State }),
                deliverables = d.Deliverables.Select(x => new { x.Id, x.Label, x.Done, x.Count }),
                nextActions = d.NextActions,
                recentActivity = d.RecentActivity,
                helpTips = d.HelpTips,
            },
        };

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));
    }

    private static string RenderMarkdownBar(int percent)
    {
        const int width = 20;
        var filled = Math.Clamp((int)Math.Round(width * percent / 100.0), 0, width);
        return "`[" + new string('█', filled) + new string('░', width - filled) + "]`";
    }

    private static (int AppScreens, int FeatureFiles, int SharedFiles, bool HasPackageJson, bool HasReadme)
        CountProjectFiles(string root)
    {
        if (!Directory.Exists(root))
            return (0, 0, 0, false, false);

        static int CountFiles(string dir) =>
            Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count()
                : 0;

        return (
            Directory.Exists(Path.Combine(root, "app"))
                ? Directory.EnumerateFiles(Path.Combine(root, "app"), "*.tsx", SearchOption.AllDirectories).Count()
                : 0,
            CountFiles(Path.Combine(root, "features")),
            CountFiles(Path.Combine(root, "shared")),
            File.Exists(Path.Combine(root, "package.json")),
            File.Exists(Path.Combine(root, "README.md")));
    }

    private static (IReadOnlyList<string> Lines, string? LastKind) ParseRecentEvidence(string json, int max)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (Array.Empty<string>(), null);

            var lines = new List<string>();
            string? lastKind = null;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var kind = item.TryGetProperty("Kind", out var k) ? k.GetString() : "?";
                var summary = item.TryGetProperty("Summary", out var s) ? s.GetString() : "";
                var next = item.TryGetProperty("NextStatus", out var n) ? n.GetString() : "";
                var at = item.TryGetProperty("At", out var a) ? a.GetString() : "";
                lastKind = kind;
                lines.Add($"`{kind}` → {next}: {summary} ({at})");
            }

            return (lines.TakeLast(max).ToList(), lastKind);
        }
        catch
        {
            return (Array.Empty<string>(), null);
        }
    }

    private static (int FileCount, DateTimeOffset? NewestWriteUtc) AnalyzeWorktreeActivity(string worktree)
    {
        if (!Directory.Exists(worktree))
            return (0, null);

        var count = 0;
        DateTimeOffset? newest = null;
        foreach (var file in Directory.EnumerateFiles(worktree, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(worktree, file);
            if (ShouldSkipRelativePath(rel))
                continue;

            count++;
            var write = File.GetLastWriteTimeUtc(file);
            var writeOffset = new DateTimeOffset(write, TimeSpan.Zero);
            if (newest is null || writeOffset > newest)
                newest = writeOffset;
        }

        return (count, newest);
    }

    private sealed record MirrorTarget(
        string MirrorRoot,
        string SessionRoot,
        bool IsSharedSessionRoot,
        Guid MirrorKeyId,
        LiveMirrorMode Mode);

}

public sealed record WorkspaceLiveSnapshot(
    DateTimeOffset UpdatedAt,
    Guid TaskId,
    Guid LeaseId,
    Guid MirrorKeyId,
    string? HermesSessionId,
    string TaskTitle,
    string LeaseStatus,
    string? BlockedReason,
    string EvidenceLogJson,
    string SessionWorkspaceRoot,
    string LiveMirrorRoot,
    string MirrorMode,
    bool IsSharedSessionRootMirror,
    string WorktreePath,
    int FilesCopiedThisTick,
    int AppScreenCount,
    int FeatureFileCount,
    int SharedFileCount,
    bool HasPackageJson,
    bool HasReadme,
    int WorktreeFileCount,
    DateTimeOffset? WorktreeLastWriteUtc,
    string? LastEvidenceKind,
    int RecommendedPollSeconds,
    IReadOnlyList<string> RecentEvidence,
    string LiveFilePath,
    WorkspaceLivePresentation.ViewModel Presentation);
