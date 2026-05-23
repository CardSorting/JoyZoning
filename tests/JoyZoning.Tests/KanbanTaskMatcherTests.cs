using JoyZoning.Agents.Hermes;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class KanbanTaskMatcherTests
{
    [Fact]
    public void FindExistingKanbanId_MatchesTitleAndWorkspace()
    {
        var root = Path.GetFullPath("/tmp/ws");
        var remote = new[]
        {
            new HermesKanbanTaskSnapshot("kb-1", "Fix build", "", "ready", null, root),
            new HermesKanbanTaskSnapshot("kb-2", "Other", "", "ready", null, "/else"),
        };

        var id = KanbanTaskMatcher.FindExistingKanbanId("Fix build", root, remote);
        Assert.Equal("kb-1", id);
    }

    [Fact]
    public void FindExistingKanbanId_IgnoresTitleOnlyWhenWorkspaceDiffers()
    {
        var root = Path.GetFullPath("/tmp/ws");
        var remote = new[]
        {
            new HermesKanbanTaskSnapshot("kb-1", "Fix build", "", "ready", null, "/other/ws"),
        };

        var id = KanbanTaskMatcher.FindExistingKanbanId("Fix build", root, remote);
        Assert.Null(id);
    }

    [Fact]
    public void FindExistingKanbanId_MatchesChildWorkspaceUnderRoot()
    {
        var root = Path.GetFullPath("/tmp/ws");
        var child = Path.Combine(root, "sub");
        var remote = new[]
        {
            new HermesKanbanTaskSnapshot("kb-1", "Task", "", "ready", null, child),
        };

        var id = KanbanTaskMatcher.FindExistingKanbanId("Task", root, remote);
        Assert.Equal("kb-1", id);
    }
}
