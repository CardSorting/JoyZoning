using JoyZoning.Adapters.Workspace;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Scans active lease worktrees and publishes git/workspace timeline deltas.</summary>
public sealed class LeaseWorktreeMonitor
{
    private readonly IExecutionLeaseRepository _leases;
    private readonly IWorkspaceAdapter _workspace;
    private readonly WorkspaceEventPublisher _publisher;
    private readonly WorkspaceLiveMirrorService _liveMirror;
    private readonly IHubContext<OperatorHub> _hub;

    public LeaseWorktreeMonitor(
        IExecutionLeaseRepository leases,
        IWorkspaceAdapter workspace,
        WorkspaceEventPublisher publisher,
        WorkspaceLiveMirrorService liveMirror,
        IHubContext<OperatorHub> hub)
    {
        _leases = leases;
        _workspace = workspace;
        _publisher = publisher;
        _liveMirror = liveMirror;
        _hub = hub;
    }

    public async Task<int> ScanActiveLeasesAsync(CancellationToken cancellationToken = default)
    {
        var active = await _leases.ListActiveAsync(cancellationToken);
        var publishedCount = 0;

        foreach (var lease in active)
        {
            if (string.IsNullOrWhiteSpace(lease.WorktreePath)
                || !Directory.Exists(lease.WorktreePath))
                continue;

            var files = await _workspace.ListChangedFilesAsync(lease.WorktreePath, cancellationToken);
            var sessionId = lease.AssignedSessionId != Guid.Empty
                ? lease.AssignedSessionId
                : lease.OperatorSessionId;

            var published = await _publisher.PublishChangedFilesAsync(
                lease.WorktreePath,
                files,
                sessionId,
                cancellationToken);

            if (!published)
                continue;

            publishedCount++;

            var live = await _liveMirror.RefreshForLeaseAsync(lease, cancellationToken);

            await _hub.Clients.All.SendAsync(
                "OnWorktreeRefreshed",
                new WorktreeRefreshedDto(
                    lease.WorkTaskId,
                    lease.WorktreePath,
                    files.Count,
                    "worktree",
                    live?.LiveFilePath),
                cancellationToken);
        }

        return publishedCount;
    }
}

public record WorktreeRefreshedDto(
    Guid TaskId,
    string WorkspaceRoot,
    int FileCount,
    string Inspect,
    string? LiveStatusPath = null);
