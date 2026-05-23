namespace JoyZoning.Domain.Orchestration;

public static class KanbanMergeRules
{
    /// <summary>
    /// Remote kanban status must not overwrite local when local changed after last sync
    /// or when a status push is still pending (revision ahead of last successful push).
    /// </summary>
    public static bool LocalStatusWins(
        bool localWinsByUpdatedAt,
        long kanbanRevision,
        long kanbanPushedRevision) =>
        localWinsByUpdatedAt || kanbanRevision > kanbanPushedRevision;
}
