using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

public class JoyEvent
{
    public long Id { get; set; }
    public Guid CorrelationId { get; set; }
    public EventSource Source { get; set; }
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; }
}
