using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Mapping;

/// <summary>
/// Maps JoyZoning task statuses to Hermes kanban statuses.
/// Hermes valid: triage, todo, scheduled, ready, running, blocked, review, done, archived
/// </summary>
public static class KanbanStatusMapping
{
    public static string ToHermesKanbanStatus(WorkTaskStatus status) => status switch
    {
        WorkTaskStatus.Backlog => "todo",
        WorkTaskStatus.Planned => "ready",
        WorkTaskStatus.InProgress => "running",
        WorkTaskStatus.NeedsApproval => "review",
        WorkTaskStatus.Verifying => "review",
        WorkTaskStatus.Blocked => "blocked",
        WorkTaskStatus.Complete => "done",
        _ => "todo",
    };

    public static WorkTaskStatus FromHermesKanbanStatus(string hermesStatus, bool hasPendingApproval = false)
    {
        if (hasPendingApproval)
            return WorkTaskStatus.NeedsApproval;

        return hermesStatus.ToLowerInvariant() switch
        {
            "triage" or "todo" => WorkTaskStatus.Backlog,
            "scheduled" or "ready" => WorkTaskStatus.Planned,
            "running" => WorkTaskStatus.InProgress,
            "review" => WorkTaskStatus.Verifying,
            "blocked" => WorkTaskStatus.Blocked,
            "done" => WorkTaskStatus.Complete,
            _ => WorkTaskStatus.Backlog,
        };
    }
}
