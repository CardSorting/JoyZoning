using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorktreePlannerTests
{
    [Fact]
    public void Worktree_uses_canonical_workspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        Assert.True(WorktreePlanner.TryPlan(root, Guid.NewGuid(), out var wt, out var branch, out _));
        Assert.StartsWith("joyzoning/card-", branch);
        Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(wt));
        Assert.DoesNotContain("worktrees", wt);
    }

    [Fact]
    public void Sandbox_check_tolerates_macos_private_var_alias()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var root = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var privateRoot = "/private" + root;

        Assert.True(WorktreePlanner.TryPlan(privateRoot, Guid.NewGuid(), out _, out _, out var error), error);
    }

    [Fact]
    public void Jsdp_bounded_session_uses_canonical_workspace_not_sandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var session = new OperatorSession
        {
            Id = Guid.NewGuid(),
            WorkspaceRoot = root,
            ExecutionMode = SessionExecutionMode.BoundedRole,
            DeliveryChainId = Guid.NewGuid(),
            DeliverySequence = 1,
        };

        Assert.True(
            WorktreePlanner.TryPlan(root, Guid.NewGuid(), null, session, out var wt, out var branch, out _));
        Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(wt));
        Assert.DoesNotContain("worktrees", wt);
        Assert.StartsWith("joyzoning/card-", branch);
    }

    [Fact]
    public void Path_traversal_card_id_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        Assert.False(WorktreePlanner.TryPlan(root, Guid.Empty, out _, out _, out var error));
        Assert.NotNull(error);
    }
}
