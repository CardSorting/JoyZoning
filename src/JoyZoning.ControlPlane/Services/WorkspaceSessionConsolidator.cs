using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Ensures one canonical operator session per physical workspace and merges orphan tasks.
/// </summary>
public sealed class WorkspaceSessionConsolidator
{
    private readonly IOperatorSessionRepository _sessions;
    private readonly IWorkTaskRepository _tasks;
    private readonly IExecutionLeaseRepository _leases;
    private readonly ILogger<WorkspaceSessionConsolidator> _logger;

    public WorkspaceSessionConsolidator(
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IExecutionLeaseRepository leases,
        ILogger<WorkspaceSessionConsolidator> logger)
    {
        _sessions = sessions;
        _tasks = tasks;
        _leases = leases;
        _logger = logger;
    }

    public async Task<OperatorSession> EnsureCanonicalAsync(
        OperatorSession session,
        CancellationToken cancellationToken = default)
    {
        var all = await _sessions.ListAsync(cancellationToken);
        var canonical = WorkspaceSessionCatalog.FindCanonicalForWorkspace(all, session.WorkspaceRoot)
            ?? session;

        if (canonical.Id == session.Id)
        {
            await ConsolidateWorkspaceAsync(canonical.WorkspaceRoot, all, cancellationToken);
            return await _sessions.GetByIdAsync(canonical.Id, cancellationToken) ?? canonical;
        }

        await ConsolidateWorkspaceAsync(session.WorkspaceRoot, all, cancellationToken);
        return await _sessions.GetByIdAsync(canonical.Id, cancellationToken) ?? canonical;
    }

    public async Task ConsolidateAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await _sessions.ListAsync(cancellationToken);
        foreach (var group in all.GroupBy(WorkspaceSessionCatalog.WorkspaceKey, StringComparer.OrdinalIgnoreCase))
            await ConsolidateGroupAsync(group.ToList(), cancellationToken);

        await BackfillWorkspaceKeysAsync(cancellationToken);
    }

    private async Task BackfillWorkspaceKeysAsync(CancellationToken cancellationToken)
    {
        var all = await _sessions.ListAsync(cancellationToken);
        foreach (var session in all)
        {
            if (!WorkspacePaths.TryNormalize(session.WorkspaceRoot, out var key))
                continue;

            if (string.Equals(session.WorkspaceKey, key, StringComparison.OrdinalIgnoreCase))
                continue;

            session.WorkspaceKey = key;
            session.WorkspaceRoot = key;
            session.UpdatedAt = DateTimeOffset.UtcNow;
            await _sessions.UpdateAsync(session, cancellationToken);
        }
    }

    private async Task ConsolidateWorkspaceAsync(
        string workspaceRoot,
        IReadOnlyList<OperatorSession> all,
        CancellationToken cancellationToken)
    {
        var group = all
            .Where(s => WorkspacePaths.EqualsNormalized(s.WorkspaceRoot, workspaceRoot))
            .ToList();
        if (group.Count <= 1)
            return;

        await ConsolidateGroupAsync(group, cancellationToken);
    }

    private async Task ConsolidateGroupAsync(
        IReadOnlyList<OperatorSession> group,
        CancellationToken cancellationToken)
    {
        if (group.Count <= 1)
            return;

        var canonical = group.OrderByDescending(s => s.UpdatedAt).First();
        var normalizedRoot = WorkspacePaths.TryNormalize(canonical.WorkspaceRoot, out var norm)
            ? norm
            : canonical.WorkspaceRoot;

        canonical.WorkspaceRoot = normalizedRoot;
        canonical.WorkspaceKey = normalizedRoot;
        canonical.UpdatedAt = DateTimeOffset.UtcNow;
        await _sessions.UpdateAsync(canonical, cancellationToken);

        foreach (var duplicate in group.Where(s => s.Id != canonical.Id))
            await MergeDuplicateSessionAsync(canonical, duplicate, cancellationToken);
    }

    private async Task MergeDuplicateSessionAsync(
        OperatorSession canonical,
        OperatorSession duplicate,
        CancellationToken cancellationToken)
    {
        var duplicateTasks = await _tasks.ListBySessionAsync(duplicate.Id, cancellationToken);
        foreach (var task in duplicateTasks)
        {
            if (!string.IsNullOrEmpty(task.HermesKanbanTaskId))
            {
                var existing = await _tasks.GetByHermesKanbanIdAsync(
                    canonical.Id, task.HermesKanbanTaskId, cancellationToken);
                if (existing is not null)
                {
                    await _leases.ReassignWorkTaskAsync(task.Id, existing.Id, cancellationToken);
                    await _tasks.DeleteAsync(task.Id, cancellationToken);
                    _logger.LogInformation(
                        "Removed duplicate task {TaskId} (kanban {KanbanId}) during session merge",
                        task.Id,
                        task.HermesKanbanTaskId);
                    continue;
                }
            }

            var titleMatch = await _tasks.FindByTitleForWorkspaceAsync(
                canonical.WorkspaceRoot, task.Title, cancellationToken);
            if (titleMatch is not null && titleMatch.Id != task.Id)
            {
                await _leases.ReassignWorkTaskAsync(task.Id, titleMatch.Id, cancellationToken);
                await _tasks.DeleteAsync(task.Id, cancellationToken);
                _logger.LogInformation(
                    "Removed duplicate task {TaskId} (title {Title}) during session merge",
                    task.Id,
                    task.Title);
                continue;
            }

            task.OperatorSessionId = canonical.Id;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            await _tasks.UpdateAsync(task, cancellationToken);
        }

        if (ShouldPreferHermesSessionId(duplicate.HermesSessionId, canonical.HermesSessionId, canonical.Id))
        {
            canonical.HermesSessionId = duplicate.HermesSessionId;
            canonical.UpdatedAt = DateTimeOffset.UtcNow;
            await _sessions.UpdateAsync(canonical, cancellationToken);
        }

        await _leases.ReassignOperatorSessionAsync(duplicate.Id, canonical.Id, cancellationToken);
        await _sessions.DeleteAsync(duplicate.Id, cancellationToken);
        _logger.LogInformation(
            "Merged duplicate session {DuplicateId} into canonical {CanonicalId} for workspace {WorkspaceRoot}",
            duplicate.Id,
            canonical.Id,
            canonical.WorkspaceRoot);
    }

    private static bool ShouldPreferHermesSessionId(
        string? candidate,
        string? current,
        Guid canonicalSessionId)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (string.IsNullOrWhiteSpace(current))
            return true;

        var defaultId = canonicalSessionId.ToString();
        return string.Equals(current, defaultId, StringComparison.Ordinal)
            && !string.Equals(candidate, defaultId, StringComparison.Ordinal);
    }
}
