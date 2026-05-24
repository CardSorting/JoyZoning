using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using JoyZoning.Adapters.Workspace;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Persists workspace/git snapshots to the event timeline and notifies Watch clients.</summary>
public class WorkspaceEventPublisher
{
    private static readonly ConcurrentDictionary<string, string> LastSnapshotHashes = new();

    private readonly IOperatorSessionRepository _sessions;
    private readonly EventIngestor _events;
    private readonly OperatorHubNotifier _hub;

    public WorkspaceEventPublisher(
        IOperatorSessionRepository sessions,
        EventIngestor events,
        OperatorHubNotifier hub)
    {
        _sessions = sessions;
        _events = events;
        _hub = hub;
    }

    /// <returns>True when a new snapshot was published to the event timeline.</returns>
    public async Task<bool> PublishChangedFilesAsync(
        string workspaceRoot,
        IReadOnlyList<ChangedFile> files,
        Guid? sessionId = null,
        Guid? taskId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            return false;

        var normalizedRoot = Path.GetFullPath(workspaceRoot);
        var dedupeKey = sessionId is { } sid && sid != Guid.Empty
            ? $"{sid:N}|{normalizedRoot}"
            : normalizedRoot;
        var hash = ComputeSnapshotHash(files);
        if (LastSnapshotHashes.TryGetValue(dedupeKey, out var previous) && previous == hash)
            return false;

        LastSnapshotHashes[dedupeKey] = hash;

        var session = await ResolveSessionAsync(normalizedRoot, sessionId, cancellationToken);
        var correlationId = session?.Id ?? Guid.Empty;
        if (correlationId == Guid.Empty)
            return false;

        var isGit = GitWorkspaceStatus.IsGitRepository(normalizedRoot);
        var payload = new
        {
            workspaceRoot = normalizedRoot,
            isGitRepo = isGit,
            fileCount = files.Count,
            files = files.Take(100).Select(f => new { f.Path, f.ChangeKind }).ToList(),
        };

        if (isGit)
        {
            await _events.IngestAsync(
                correlationId,
                EventSource.Git,
                EventTypes.GitStatusChanged,
                payload,
                cancellationToken);
        }

        foreach (var file in files.Take(50))
        {
            await _events.IngestAsync(
                correlationId,
                EventSource.Workspace,
                EventTypes.WorkspaceFileChanged,
                new { workspaceRoot = normalizedRoot, file.Path, file.ChangeKind },
                cancellationToken);
        }

        if (taskId is { } tid && tid != Guid.Empty)
            await NotifyTaskWorkspaceAsync(tid, normalizedRoot, files.Count, cancellationToken);

        return true;
    }

    private async Task NotifyTaskWorkspaceAsync(
        Guid taskId,
        string workspaceRoot,
        int fileCount,
        CancellationToken cancellationToken)
    {
        await _hub.BroadcastAsync(
            "OnWorktreeRefreshed",
            new
            {
                taskId,
                workspaceRoot,
                fileCount,
                inspect = "canonical",
            },
            cancellationToken);

        await _hub.BroadcastAsync(
            "OnTaskLiveUpdated",
            new TaskLiveUpdatedDto(
                taskId,
                LeaseStatus: string.Empty,
                Headline: $"{fileCount} changed file(s)",
                ProgressPercent: 0,
                FilesCopiedThisTick: fileCount,
                UpdatedAt: DateTimeOffset.UtcNow,
                WorkspacePath: workspaceRoot),
            cancellationToken);
    }

    internal static void ClearDedupeCacheForTests() => LastSnapshotHashes.Clear();

    private async Task<Domain.Entities.OperatorSession?> ResolveSessionAsync(
        string normalizedRoot,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId is { } id && id != Guid.Empty)
        {
            var direct = await _sessions.GetByIdAsync(id, cancellationToken);
            if (direct is not null)
                return direct;
        }

        var sessions = await _sessions.ListAsync(cancellationToken);
        return sessions
            .Where(s => !string.IsNullOrWhiteSpace(s.WorkspaceRoot))
            .Where(s => PathsEqual(s.WorkspaceRoot, normalizedRoot))
            .OrderByDescending(s => s.UpdatedAt.UtcDateTime)
            .FirstOrDefault();
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeSnapshotHash(IReadOnlyList<ChangedFile> files)
    {
        var sb = new StringBuilder();
        foreach (var f in files.OrderBy(x => x.Path, StringComparer.Ordinal))
            sb.Append(f.Path).Append('|').Append(f.ChangeKind).Append('\n');

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
