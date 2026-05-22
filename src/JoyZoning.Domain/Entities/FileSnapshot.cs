using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Entities;

public class FileSnapshot
{
    public Guid Id { get; set; }
    public Guid? WorkTaskId { get; set; }
    public string Path { get; set; } = string.Empty;
    public FileChangeKind ChangeKind { get; set; }
    public string? DiffRef { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public WorkTask? WorkTask { get; set; }
}
