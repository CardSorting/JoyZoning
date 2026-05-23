using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

public class KanbanMergeRulesTests
{
    [Fact]
    public void LocalStatusWins_when_pending_push_revision()
    {
        Assert.True(KanbanMergeRules.LocalStatusWins(
            localWinsByUpdatedAt: false,
            kanbanRevision: 3,
            kanbanPushedRevision: 2));
    }

    [Fact]
    public void LocalStatusWins_when_updated_after_last_sync()
    {
        Assert.True(KanbanMergeRules.LocalStatusWins(
            localWinsByUpdatedAt: true,
            kanbanRevision: 1,
            kanbanPushedRevision: 1));
    }

    [Fact]
    public void Remote_may_apply_when_no_local_pending_and_not_recent()
    {
        Assert.False(KanbanMergeRules.LocalStatusWins(
            localWinsByUpdatedAt: false,
            kanbanRevision: 5,
            kanbanPushedRevision: 5));
    }
}
