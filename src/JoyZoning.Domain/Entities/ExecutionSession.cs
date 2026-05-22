using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

public class ExecutionSession
{
    public Guid Id { get; set; }
    public Guid WorkTaskId { get; set; }
    public string HermesRunId { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string? ActivePlanJson { get; set; }
    public ExecutionPhase Phase { get; set; } = ExecutionPhase.Starting;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public WorkTask? WorkTask { get; set; }
    public ICollection<ExecutionStep> Steps { get; set; } = new List<ExecutionStep>();
    public ICollection<TerminalSession> TerminalSessions { get; set; } = new List<TerminalSession>();
}
