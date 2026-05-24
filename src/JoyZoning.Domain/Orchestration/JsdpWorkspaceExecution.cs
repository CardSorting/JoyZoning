using JoyZoning.Domain.Entities;

namespace JoyZoning.Domain.Orchestration;

/// <summary>JSDP workspace rules — canonical session root only; no sandboxes or live mirrors.</summary>
public static class JsdpWorkspaceExecution
{
    public const string CanonicalStrategy = "canonical_inplace";

    public static bool IsCanonicalWorktree(string workspaceRoot, string worktreePath)
    {
        if (!WorkspacePaths.TryNormalize(workspaceRoot, out var root)
            || !WorkspacePaths.TryNormalize(worktreePath, out var wt))
            return false;

        return root.Equals(wt, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryAlignLeaseToCanonical(
        ExecutionLease lease,
        OperatorSession session,
        WorkTask task,
        out HandoffPacket? handoff)
    {
        handoff = null;
        if (!WorktreePlanner.TryPlan(
                session.WorkspaceRoot,
                task.Id,
                task.HermesKanbanTaskId,
                session,
                out var canonicalPath,
                out var branchName,
                out _))
            return false;

        if (IsCanonicalWorktree(session.WorkspaceRoot, lease.WorktreePath))
            return false;

        lease.WorktreePath = canonicalPath;
        lease.BranchName = branchName;
        handoff = HandoffPacketBuilder.Build(
            task,
            canonicalPath,
            branchName,
            session.WorkspaceRoot,
            new WorktreeSeeder.SeedResult(0, SkippedExistingContent: true),
            session);
        lease.HandoffPacketJson = HandoffPacketBuilder.Serialize(handoff);
        return true;
    }

    public static int PruneLegacyMirrorArtifacts(string workspaceRoot)
    {
        if (!WorkspacePaths.TryNormalize(workspaceRoot, out var root))
            return 0;

        var removed = 0;
        removed += TryDeleteTree(Path.Combine(root, ".joyzoning", "worktrees"));
        removed += TryDeleteTree(Path.Combine(root, ".joyzoning", "live"));
        return removed;
    }

    private static int TryDeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        try
        {
            Directory.Delete(path, recursive: true);
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}
