using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

public class WorktreePlannerTests
{
    [Fact]
    public void Worktree_stays_under_sandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        Assert.True(WorktreePlanner.TryPlan(root, Guid.NewGuid(), out var wt, out var branch, out _));
        Assert.StartsWith("joyzoning/card-", branch);
        Assert.Contains(".joyzoning", wt);
        Assert.Contains("worktrees", wt);
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
