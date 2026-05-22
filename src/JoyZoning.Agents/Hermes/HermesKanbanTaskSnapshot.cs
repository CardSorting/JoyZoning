namespace JoyZoning.Agents.Hermes;

public record HermesKanbanTaskSnapshot(
    string Id,
    string Title,
    string Body,
    string Status,
    string? Assignee,
    string? WorkspacePath);
