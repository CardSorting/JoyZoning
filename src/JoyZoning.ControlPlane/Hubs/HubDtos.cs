namespace JoyZoning.ControlPlane.Hubs;

public record ManagerChatDeltaDto(Guid SessionId, string Delta);
public record ManagerChatCompleteDto(Guid SessionId);
public record TerminalOutputDto(Guid CorrelationId, string Text);

public record TaskLiveUpdatedDto(
    Guid TaskId,
    string LeaseStatus,
    string Headline,
    int ProgressPercent,
    int FilesCopiedThisTick,
    DateTimeOffset UpdatedAt,
    string? WorkspacePath = null,
    string? MirrorMode = null);

public record CodeActivityDto(Guid TaskId, string Path, string Kind, string? Preview);
