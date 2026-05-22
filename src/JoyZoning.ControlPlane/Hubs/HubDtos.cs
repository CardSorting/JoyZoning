namespace JoyZoning.ControlPlane.Hubs;

public record ManagerChatDeltaDto(Guid SessionId, string Delta);
public record ManagerChatCompleteDto(Guid SessionId);
public record TerminalOutputDto(Guid CorrelationId, string Text);
