using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class WorktreePlannerKanbanTests
{
    [Fact]
    public void TryPlan_uses_same_worktree_for_same_hermes_kanban_id()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var cardA = Guid.NewGuid();
        var cardB = Guid.NewGuid();
        const string kanbanId = "kb-stable-123";

        Assert.True(WorktreePlanner.TryPlan(root, cardA, kanbanId, out var wtA, out _, out _));
        Assert.True(WorktreePlanner.TryPlan(root, cardB, kanbanId, out var wtB, out _, out _));
        Assert.Equal(wtA, wtB);
    }

    [Fact]
    public void TryPlan_differs_without_hermes_kanban_id()
    {
        var root = Path.Combine(Path.GetTempPath(), "jz-wt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        Assert.True(WorktreePlanner.TryPlan(root, Guid.NewGuid(), out var wtA, out _, out _));
        Assert.True(WorktreePlanner.TryPlan(root, Guid.NewGuid(), out var wtB, out _, out _));
        Assert.NotEqual(wtA, wtB);
    }
}
