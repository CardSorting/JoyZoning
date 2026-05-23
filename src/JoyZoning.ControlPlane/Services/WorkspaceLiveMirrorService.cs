using System.Text;
using System.Text.Json;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Orchestration;
using JoyZoning.Persistence.Repositories;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Mirrors lease worktree output to the session workspace root and maintains JOYZONING_LIVE.md.
/// </summary>
public sealed class WorkspaceLiveMirrorService
{
    private static readonly HashSet<string> ExcludedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".joyzoning", ".claude", ".vscode", ".expo", "dist", "build",
    };

    private readonly IOperatorSessionRepository _sessions;
    private readonly IWorkTaskRepository _tasks;
    private readonly WorkspaceOptions _options;
    private readonly ILogger<WorkspaceLiveMirrorService> _logger;

    public WorkspaceLiveMirrorService(
        IOperatorSessionRepository sessions,
        IWorkTaskRepository tasks,
        IOptions<WorkspaceOptions> options,
        ILogger<WorkspaceLiveMirrorService> logger)
    {
        _sessions = sessions;
        _tasks = tasks;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WorkspaceLiveSnapshot?> RefreshForLeaseAsync(
        ExecutionLease lease,
        CancellationToken cancellationToken = default)
    {
        if (!_options.MirrorToSessionRoot)
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

        var filesCopied = MirrorDirectory(worktree, sessionRoot);
        var snapshot = BuildSnapshot(task, lease, sessionRoot, worktree, filesCopied);
        WriteLiveFile(sessionRoot, snapshot);
        WriteLiveJson(sessionRoot, snapshot);
        return snapshot;
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

    private WorkspaceLiveSnapshot BuildSnapshot(
        WorkTask task,
        ExecutionLease lease,
        string sessionRoot,
        string worktree,
        int filesCopied)
    {
        var counts = CountProjectFiles(sessionRoot);
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
            TaskTitle: task.Title,
            LeaseStatus: status,
            BlockedReason: lease.BlockedReason,
            EvidenceLogJson: lease.EvidenceLogJson,
            SessionWorkspaceRoot: sessionRoot,
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
            LiveFilePath: Path.Combine(sessionRoot, _options.LiveStatusFileName),
            Presentation: presentation);
    }

    private void WriteLiveFile(string sessionRoot, WorkspaceLiveSnapshot snapshot)
    {
        var path = Path.Combine(sessionRoot, _options.LiveStatusFileName);
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
            sb.AppendLine($"\n*{snapshot.FilesCopiedThisTick} file(s) synced to your folder this update.*");

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
        sb.AppendLine($"- **Status:** `{snapshot.LeaseStatus}`");
        if (!string.IsNullOrWhiteSpace(snapshot.BlockedReason))
            sb.AppendLine($"- **Blocked:** {snapshot.BlockedReason}");
        sb.AppendLine($"- **Canonical build folder:** `{snapshot.WorktreePath}`");
        sb.AppendLine($"- **Your IDE folder (mirrored copy):** `{snapshot.SessionWorkspaceRoot}`");
        sb.AppendLine();
        sb.AppendLine("JoyZoning builds in `.joyzoning/worktrees/<task>/` and mirrors files here automatically.");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("jz task lease " + snapshot.TaskId);
        sb.AppendLine("open \"" + snapshot.SessionWorkspaceRoot + "\"");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("</details>");
        sb.AppendLine();
        sb.AppendLine("_Auto-generated by JoyZoning. This file is overwritten while the task runs._");

        File.WriteAllText(path, sb.ToString());
    }

    private void WriteLiveJson(string sessionRoot, WorkspaceLiveSnapshot snapshot)
    {
        if (!_options.WriteLiveJsonFile)
            return;

        var jsonPath = Path.Combine(sessionRoot, _options.LiveJsonRelativePath);
        var parent = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var d = snapshot.Presentation;
        var body = new
        {
            updatedAt = snapshot.UpdatedAt,
            taskId = snapshot.TaskId,
            title = snapshot.TaskTitle,
            leaseStatus = snapshot.LeaseStatus,
            liveMarkdown = snapshot.LiveFilePath,
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

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
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
}

public sealed record WorkspaceLiveSnapshot(
    DateTimeOffset UpdatedAt,
    Guid TaskId,
    string TaskTitle,
    string LeaseStatus,
    string? BlockedReason,
    string EvidenceLogJson,
    string SessionWorkspaceRoot,
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
