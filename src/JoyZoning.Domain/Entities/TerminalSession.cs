namespace JoyZoning.Domain.Entities;

public class TerminalSession
{
    public Guid Id { get; set; }
    public Guid? ExecutionSessionId { get; set; }
    public string Backend { get; set; } = "local";
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ExecutionSession? ExecutionSession { get; set; }
}
