using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Adapters.Workspace;

/// <summary>Squash-merge worker branch or apply worktree diff into the canonical workspace.</summary>
public sealed class WorkspaceGitMerger : IWorkspaceGitMerger
{
    private const string StrategySquashBranch = "squash_branch";
    private const string StrategyPatchApply = "patch_apply";
    private const string StrategyFilesystemCopy = "filesystem_copy";
    private const string StrategyCanonicalInPlace = JsdpWorkspaceExecution.CanonicalStrategy;

    public Task<WorkspaceConvergenceResult> PreflightAsync(
        WorkspaceConvergenceRequest request,
        CancellationToken cancellationToken = default) =>
        ConvergeInternalAsync(request with { DryRun = true }, cancellationToken);

    public Task<WorkspaceConvergenceResult> ConvergeAsync(
        WorkspaceConvergenceRequest request,
        CancellationToken cancellationToken = default) =>
        ConvergeInternalAsync(request, cancellationToken);

    private static async Task<WorkspaceConvergenceResult> ConvergeInternalAsync(
        WorkspaceConvergenceRequest request,
        CancellationToken cancellationToken)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var dest = Path.GetFullPath(request.DestinationWorkspaceRoot.Trim());
        var worktree = Path.GetFullPath(request.WorkerWorktreePath.Trim());
        var branch = request.WorkerBranchName.Trim();

        WorkspaceConvergenceResult Fail(
            string strategy,
            string message,
            IReadOnlyList<string>? conflicts = null,
            string? sourceHead = null,
            string? destPrev = null) =>
            new(
                Succeeded: false,
                HadConflicts: conflicts is { Count: > 0 },
                Strategy: strategy,
                ErrorMessage: message,
                SourceWorktreePath: worktree,
                SourceBranch: branch,
                SourceHeadCommit: sourceHead,
                DestinationWorkspaceRoot: dest,
                DestinationBranch: null,
                DestinationPreviousHead: destPrev,
                DestinationNewHead: null,
                ChangedFiles: Array.Empty<string>(),
                ConflictFiles: conflicts ?? Array.Empty<string>(),
                CompletedAt: completedAt);

        if (!Directory.Exists(dest))
            return Fail(StrategySquashBranch, "Canonical workspace path does not exist.");

        if (!GitWorkspaceStatus.IsGitRepository(dest))
            return Fail(
                StrategySquashBranch,
                "Canonical workspace is not a git repository; cannot converge code.");

        var destBranch = await GitCommandRunner.RunAsync(
            dest,
            "rev-parse --abbrev-ref HEAD",
            cancellationToken);
        var destBranchName = destBranch.ExitCode == 0 ? destBranch.StdOut.Trim() : null;
        var destPrevHead = await GitCommandRunner.ReadRevAsync(dest, "HEAD", cancellationToken);

        if (destPrevHead is null)
            return Fail(StrategySquashBranch, "Could not resolve HEAD in canonical workspace.");

        if (JsdpWorkspaceExecution.IsCanonicalWorktree(dest, worktree))
        {
            var onBranch = destBranchName?.Trim();
            if (string.Equals(onBranch, branch, StringComparison.Ordinal))
            {
                var changed = await GitWorkspaceStatus.TryListChangedFilesAsync(dest, cancellationToken);
                return Success(
                    StrategyCanonicalInPlace,
                    worktree,
                    branch,
                    destPrevHead,
                    dest,
                    destBranchName,
                    destPrevHead,
                    destPrevHead,
                    changed.Files.Select(f => f.Path).ToList(),
                    completedAt);
            }
        }

        if (!request.AllowDirtyDestination)
        {
            var dirty = await GitWorkspaceStatus.TryListChangedFilesAsync(dest, cancellationToken);
            var blocking = FilterBlockingDirtyFiles(dirty.Files);
            if (dirty.GitSucceeded && blocking.Count > 0)
            {
                return Fail(
                    StrategySquashBranch,
                    "Canonical workspace has uncommitted changes; commit or stash before accepting.",
                    blocking,
                    destPrev: destPrevHead);
            }
        }

        var branchRef = await GitCommandRunner.RunAsync(
            dest,
            $"rev-parse --verify refs/heads/{branch}",
            cancellationToken);

        if (branchRef.ExitCode == 0)
        {
            var sourceHead = branchRef.StdOut.Trim();
            return await SquashBranchAsync(
                request,
                dest,
                worktree,
                branch,
                destBranchName,
                destPrevHead,
                sourceHead,
                completedAt,
                cancellationToken);
        }

        var summary = await GitWorkspaceStatus.TryGetWorktreeSummaryAsync(worktree, dest, cancellationToken);
        if (summary is not null
            && !string.IsNullOrWhiteSpace(summary.HeadCommit)
            && !string.IsNullOrWhiteSpace(summary.BaseCommit)
            && summary.HeadCommit != summary.BaseCommit)
        {
            return await PatchApplyAsync(
                request,
                dest,
                worktree,
                branch,
                destBranchName,
                destPrevHead,
                summary.BaseCommit,
                summary.HeadCommit,
                completedAt,
                cancellationToken);
        }

        return await FilesystemCopyAsync(
            request,
            dest,
            worktree,
            branch,
            destBranchName,
            destPrevHead,
            completedAt,
            cancellationToken);
    }

    private static async Task<WorkspaceConvergenceResult> SquashBranchAsync(
        WorkspaceConvergenceRequest request,
        string dest,
        string worktree,
        string branch,
        string? destBranchName,
        string destPrevHead,
        string sourceHead,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        if (request.DryRun)
        {
            var diffNames = await GitCommandRunner.RunAsync(
                dest,
                $"diff --name-only {destPrevHead} {sourceHead}",
                cancellationToken);
            var files = ParseLines(diffNames.StdOut);
            return Success(
                StrategySquashBranch,
                worktree,
                branch,
                sourceHead,
                dest,
                destBranchName,
                destPrevHead,
                destPrevHead,
                files,
                completedAt);
        }

        var merge = await GitCommandRunner.RunAsync(dest, $"merge --squash {branch}", cancellationToken);
        if (merge.ExitCode != 0)
        {
            var conflicts = await ListConflictPathsAsync(dest, cancellationToken);
            await TryRunQuietAsync(dest, "merge --abort", cancellationToken);
            await TryRunQuietAsync(dest, "reset --hard HEAD", cancellationToken);
            return new WorkspaceConvergenceResult(
                Succeeded: false,
                HadConflicts: true,
                Strategy: StrategySquashBranch,
                ErrorMessage: string.IsNullOrWhiteSpace(merge.StdErr)
                    ? "Squash merge failed due to conflicts."
                    : merge.StdErr.Trim(),
                SourceWorktreePath: worktree,
                SourceBranch: branch,
                SourceHeadCommit: sourceHead,
                DestinationWorkspaceRoot: dest,
                DestinationBranch: destBranchName,
                DestinationPreviousHead: destPrevHead,
                DestinationNewHead: null,
                ChangedFiles: Array.Empty<string>(),
                ConflictFiles: conflicts,
                CompletedAt: completedAt);
        }

        var stagedDiff = await GitCommandRunner.RunAsync(dest, "diff --cached --name-only", cancellationToken);
        var changed = ParseLines(stagedDiff.StdOut);

        if (changed.Count > 0)
        {
            var message = BuildCommitMessage(branch, sourceHead, destPrevHead, StrategySquashBranch);
            var commit = await GitCommandRunner.RunAsync(dest, $"commit -m {message}", cancellationToken);
            if (commit.ExitCode != 0)
            {
                return new WorkspaceConvergenceResult(
                    Succeeded: false,
                    HadConflicts: false,
                    Strategy: StrategySquashBranch,
                    ErrorMessage: string.IsNullOrWhiteSpace(commit.StdErr)
                        ? "Squash merge succeeded but commit failed."
                        : commit.StdErr.Trim(),
                    SourceWorktreePath: worktree,
                    SourceBranch: branch,
                    SourceHeadCommit: sourceHead,
                    DestinationWorkspaceRoot: dest,
                    DestinationBranch: destBranchName,
                    DestinationPreviousHead: destPrevHead,
                    DestinationNewHead: null,
                    ChangedFiles: changed,
                    ConflictFiles: Array.Empty<string>(),
                    CompletedAt: completedAt);
            }
        }

        var newHead = await GitCommandRunner.ReadRevAsync(dest, "HEAD", cancellationToken) ?? destPrevHead;
        return Success(
            StrategySquashBranch,
            worktree,
            branch,
            sourceHead,
            dest,
            destBranchName,
            destPrevHead,
            newHead,
            changed,
            completedAt);
    }

    private static async Task<WorkspaceConvergenceResult> PatchApplyAsync(
        WorkspaceConvergenceRequest request,
        string dest,
        string worktree,
        string branch,
        string? destBranchName,
        string destPrevHead,
        string baseCommit,
        string sourceHead,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        var diff = await GitCommandRunner.RunAsync(
            worktree,
            $"diff {baseCommit}..{sourceHead}",
            cancellationToken);

        if (diff.ExitCode != 0 || string.IsNullOrWhiteSpace(diff.StdOut))
        {
            return new WorkspaceConvergenceResult(
                Succeeded: false,
                HadConflicts: false,
                Strategy: StrategyPatchApply,
                ErrorMessage: "Could not compute patch between base and worker HEAD.",
                SourceWorktreePath: worktree,
                SourceBranch: branch,
                SourceHeadCommit: sourceHead,
                DestinationWorkspaceRoot: dest,
                DestinationBranch: destBranchName,
                DestinationPreviousHead: destPrevHead,
                DestinationNewHead: null,
                ChangedFiles: Array.Empty<string>(),
                ConflictFiles: Array.Empty<string>(),
                CompletedAt: completedAt);
        }

        var patchFile = Path.Combine(Path.GetTempPath(), $"jz-converge-{Guid.NewGuid():N}.patch");
        try
        {
            await File.WriteAllTextAsync(patchFile, diff.StdOut, cancellationToken);

            var check = await GitCommandRunner.RunAsync(
                dest,
                $"apply --check \"{patchFile}\"",
                cancellationToken);

            if (check.ExitCode != 0)
            {
                var conflicts = ParseLines(check.StdErr);
                return new WorkspaceConvergenceResult(
                    Succeeded: false,
                    HadConflicts: true,
                    Strategy: StrategyPatchApply,
                    ErrorMessage: "Patch would not apply cleanly to canonical workspace.",
                    SourceWorktreePath: worktree,
                    SourceBranch: branch,
                    SourceHeadCommit: sourceHead,
                    DestinationWorkspaceRoot: dest,
                    DestinationBranch: destBranchName,
                    DestinationPreviousHead: destPrevHead,
                    DestinationNewHead: null,
                    ChangedFiles: Array.Empty<string>(),
                    ConflictFiles: conflicts.Count > 0 ? conflicts : ParseLines(check.StdOut),
                    CompletedAt: completedAt);
            }

            if (request.DryRun)
            {
                var names = await GitCommandRunner.RunAsync(
                    dest,
                    $"diff --name-only {baseCommit}..{sourceHead}",
                    cancellationToken);
                return Success(
                    StrategyPatchApply,
                    worktree,
                    branch,
                    sourceHead,
                    dest,
                    destBranchName,
                    destPrevHead,
                    destPrevHead,
                    ParseLines(names.StdOut),
                    completedAt);
            }

            var apply = await GitCommandRunner.RunAsync(dest, $"apply \"{patchFile}\"", cancellationToken);
            if (apply.ExitCode != 0)
            {
                return new WorkspaceConvergenceResult(
                    Succeeded: false,
                    HadConflicts: true,
                    Strategy: StrategyPatchApply,
                    ErrorMessage: apply.StdErr.Trim(),
                    SourceWorktreePath: worktree,
                    SourceBranch: branch,
                    SourceHeadCommit: sourceHead,
                    DestinationWorkspaceRoot: dest,
                    DestinationBranch: destBranchName,
                    DestinationPreviousHead: destPrevHead,
                    DestinationNewHead: null,
                    ChangedFiles: Array.Empty<string>(),
                    ConflictFiles: ParseLines(apply.StdErr),
                    CompletedAt: completedAt);
            }

            var add = await GitCommandRunner.RunAsync(dest, "add -A", cancellationToken);
            if (add.ExitCode != 0)
            {
                return new WorkspaceConvergenceResult(
                    Succeeded: false,
                    HadConflicts: false,
                    Strategy: StrategyPatchApply,
                    ErrorMessage: "Patch applied but staging failed.",
                    SourceWorktreePath: worktree,
                    SourceBranch: branch,
                    SourceHeadCommit: sourceHead,
                    DestinationWorkspaceRoot: dest,
                    DestinationBranch: destBranchName,
                    DestinationPreviousHead: destPrevHead,
                    DestinationNewHead: null,
                    ChangedFiles: Array.Empty<string>(),
                    ConflictFiles: Array.Empty<string>(),
                    CompletedAt: completedAt);
            }

            var staged = await GitCommandRunner.RunAsync(dest, "diff --cached --name-only", cancellationToken);
            var changed = ParseLines(staged.StdOut);
            var message = BuildCommitMessage(branch, sourceHead, destPrevHead, StrategyPatchApply);
            var commit = await GitCommandRunner.RunAsync(dest, $"commit -m {message}", cancellationToken);
            if (commit.ExitCode != 0 && changed.Count > 0)
            {
                return new WorkspaceConvergenceResult(
                    Succeeded: false,
                    HadConflicts: false,
                    Strategy: StrategyPatchApply,
                    ErrorMessage: commit.StdErr.Trim(),
                    SourceWorktreePath: worktree,
                    SourceBranch: branch,
                    SourceHeadCommit: sourceHead,
                    DestinationWorkspaceRoot: dest,
                    DestinationBranch: destBranchName,
                    DestinationPreviousHead: destPrevHead,
                    DestinationNewHead: null,
                    ChangedFiles: changed,
                    ConflictFiles: Array.Empty<string>(),
                    CompletedAt: completedAt);
            }

            var newHead = await GitCommandRunner.ReadRevAsync(dest, "HEAD", cancellationToken) ?? destPrevHead;
            return Success(
                StrategyPatchApply,
                worktree,
                branch,
                sourceHead,
                dest,
                destBranchName,
                destPrevHead,
                newHead,
                changed,
                completedAt);
        }
        finally
        {
            try
            {
                if (File.Exists(patchFile))
                    File.Delete(patchFile);
            }
            catch
            {
                // best-effort temp cleanup
            }
        }
    }

    private static async Task<WorkspaceConvergenceResult> FilesystemCopyAsync(
        WorkspaceConvergenceRequest request,
        string dest,
        string worktree,
        string branch,
        string? destBranchName,
        string destPrevHead,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(worktree))
        {
            return new WorkspaceConvergenceResult(
                Succeeded: false,
                HadConflicts: false,
                Strategy: StrategyFilesystemCopy,
                ErrorMessage: "Worker worktree directory does not exist on disk.",
                SourceWorktreePath: worktree,
                SourceBranch: branch,
                SourceHeadCommit: null,
                DestinationWorkspaceRoot: dest,
                DestinationBranch: destBranchName,
                DestinationPreviousHead: destPrevHead,
                DestinationNewHead: null,
                ChangedFiles: Array.Empty<string>(),
                ConflictFiles: Array.Empty<string>(),
                CompletedAt: completedAt);
        }

        var copied = new List<string>();
        foreach (var file in Directory.EnumerateFiles(worktree, "*", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
                continue;

            var rel = Path.GetRelativePath(worktree, file);
            var target = Path.Combine(dest, rel);
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            if (!request.DryRun)
                File.Copy(file, target, overwrite: true);

            copied.Add(rel.Replace('\\', '/'));
        }

        if (copied.Count == 0)
        {
            return new WorkspaceConvergenceResult(
                Succeeded: false,
                HadConflicts: false,
                Strategy: StrategyFilesystemCopy,
                ErrorMessage: "No files found in worker worktree to apply.",
                SourceWorktreePath: worktree,
                SourceBranch: branch,
                SourceHeadCommit: null,
                DestinationWorkspaceRoot: dest,
                DestinationBranch: destBranchName,
                DestinationPreviousHead: destPrevHead,
                DestinationNewHead: null,
                ChangedFiles: Array.Empty<string>(),
                ConflictFiles: Array.Empty<string>(),
                CompletedAt: completedAt);
        }

        if (request.DryRun)
        {
            return Success(
                StrategyFilesystemCopy,
                worktree,
                branch,
                null,
                dest,
                destBranchName,
                destPrevHead,
                destPrevHead,
                copied,
                completedAt);
        }

        await GitCommandRunner.RunAsync(dest, "add -A", cancellationToken);
        var message = BuildCommitMessage(branch, null, destPrevHead, StrategyFilesystemCopy);
        var commit = await GitCommandRunner.RunAsync(dest, $"commit -m {message}", cancellationToken);
        if (commit.ExitCode != 0)
        {
            return new WorkspaceConvergenceResult(
                Succeeded: false,
                HadConflicts: false,
                Strategy: StrategyFilesystemCopy,
                ErrorMessage: commit.StdErr.Trim(),
                SourceWorktreePath: worktree,
                SourceBranch: branch,
                SourceHeadCommit: null,
                DestinationWorkspaceRoot: dest,
                DestinationBranch: destBranchName,
                DestinationPreviousHead: destPrevHead,
                DestinationNewHead: null,
                ChangedFiles: copied,
                ConflictFiles: Array.Empty<string>(),
                CompletedAt: completedAt);
        }

        var newHead = await GitCommandRunner.ReadRevAsync(dest, "HEAD", cancellationToken) ?? destPrevHead;
        return Success(
            StrategyFilesystemCopy,
            worktree,
            branch,
            null,
            dest,
            destBranchName,
            destPrevHead,
            newHead,
            copied,
            completedAt);
    }

    private static WorkspaceConvergenceResult Success(
        string strategy,
        string worktree,
        string branch,
        string? sourceHead,
        string dest,
        string? destBranch,
        string destPrevHead,
        string destNewHead,
        IReadOnlyList<string> changed,
        DateTimeOffset completedAt) =>
        new(
            Succeeded: true,
            HadConflicts: false,
            Strategy: strategy,
            ErrorMessage: null,
            SourceWorktreePath: worktree,
            SourceBranch: branch,
            SourceHeadCommit: sourceHead,
            DestinationWorkspaceRoot: dest,
            DestinationBranch: destBranch,
            DestinationPreviousHead: destPrevHead,
            DestinationNewHead: destNewHead,
            ChangedFiles: changed,
            ConflictFiles: Array.Empty<string>(),
            CompletedAt: completedAt);

    private static async Task<IReadOnlyList<string>> ListConflictPathsAsync(
        string dest,
        CancellationToken cancellationToken)
    {
        var unmerged = await GitWorkspaceStatus.TryListChangedFilesAsync(dest, cancellationToken);
        return unmerged.Files
            .Where(f => string.Equals(f.ChangeKind, "unmerged", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.Path)
            .ToList();
    }

    private static async Task TryRunQuietAsync(string dest, string args, CancellationToken cancellationToken) =>
        _ = await GitCommandRunner.RunAsync(dest, args, cancellationToken);

    private static List<string> ParseLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

    private static string BuildCommitMessage(
        string branch,
        string? sourceHead,
        string destPrevHead,
        string strategy) =>
        $"joyzoning-accept-{strategy}-branch-{branch.Replace('/', '-')}-source-{sourceHead ?? "na"}";

    /// <summary>Legacy sandbox paths under <c>.joyzoning/</c> are ignored for merge blocking.</summary>
    internal static List<string> FilterBlockingDirtyFiles(IReadOnlyList<ChangedFile> files)
    {
        return files
            .Where(f => !IsLegacySandboxPath(f.Path))
            .Select(f => f.Path)
            .ToList();
    }

    private static bool IsLegacySandboxPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/.joyzoning/worktrees/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".joyzoning/worktrees/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.joyzoning/live/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".joyzoning/live/", StringComparison.OrdinalIgnoreCase);
    }
}
