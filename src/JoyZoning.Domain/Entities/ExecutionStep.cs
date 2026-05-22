namespace JoyZoning.Domain.Entities;

public class ExecutionStep
{
    public Guid Id { get; set; }
    public Guid ExecutionSessionId { get; set; }
    public int Ordinal { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? DetailJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ExecutionSession? ExecutionSession { get; set; }
}
