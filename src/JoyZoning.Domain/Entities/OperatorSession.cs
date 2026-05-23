using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

public class OperatorSession
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorkspaceRoot { get; set; } = string.Empty;
    /// <summary>Normalized workspace path key — one row per physical folder.</summary>
    public string? WorkspaceKey { get; set; }
    public string? HermesProfile { get; set; }
    public string? HermesSessionId { get; set; }
    public Guid? ActiveTaskId { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Idle;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<WorkTask> Tasks { get; set; } = new List<WorkTask>();
}
