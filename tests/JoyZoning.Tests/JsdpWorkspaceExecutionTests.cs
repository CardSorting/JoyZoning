using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class JsdpWorkspaceExecutionTests
{
    [Fact]
    public void Bounded_role_uses_canonical_worktree_not_sandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-jsdp-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var session = BoundedSession(root, 1);

        Assert.True(WorktreePlanner.TryPlan(root, Guid.NewGuid(), null, session, out var wt, out _, out _));
        Assert.True(JsdpWorkspaceExecution.IsCanonicalWorktree(root, wt));
        Assert.DoesNotContain("worktrees", wt);
    }

    [Fact]
    public void TryAlignLeaseToCanonical_rewrites_legacy_sandbox_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-jsdp-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var session = BoundedSession(root, 2);
        var task = new WorkTask { Id = Guid.NewGuid(), Title = "Role 2 — Architecture Lock" };
        var lease = new ExecutionLease
        {
            Id = Guid.NewGuid(),
            WorkTaskId = task.Id,
            WorktreePath = Path.Combine(root, ".joyzoning", "worktrees", "deadbeef"),
            BranchName = "joyzoning/card-deadbeef",
            HandoffPacketJson = "{}",
        };

        Assert.True(JsdpWorkspaceExecution.TryAlignLeaseToCanonical(lease, session, task, out var handoff));
        Assert.NotNull(handoff);
        Assert.True(JsdpWorkspaceExecution.IsCanonicalWorktree(root, lease.WorktreePath));
        Assert.Equal(root, handoff!.SessionWorkspaceRoot);
    }

    [Fact]
    public void PruneLegacyMirrorArtifacts_removes_worktrees_and_live()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-jsdp-ws-" + Guid.NewGuid().ToString("N"));
        var worktrees = Path.Combine(root, ".joyzoning", "worktrees", "abc");
        var live = Path.Combine(root, ".joyzoning", "live", Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(worktrees);
        Directory.CreateDirectory(live);
        File.WriteAllText(Path.Combine(worktrees, "stale.txt"), "x");

        Assert.Equal(2, JsdpWorkspaceExecution.PruneLegacyMirrorArtifacts(root));
        Assert.False(Directory.Exists(worktrees));
        Assert.False(Directory.Exists(Path.Combine(root, ".joyzoning", "live")));
    }

    private static OperatorSession BoundedSession(string root, int sequence) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkspaceRoot = root,
            ExecutionMode = SessionExecutionMode.BoundedRole,
            DeliveryChainId = Guid.NewGuid(),
            DeliverySequence = sequence,
        };
}
