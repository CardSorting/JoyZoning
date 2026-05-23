using JoyZoning.Domain.Enums;

namespace JoyZoning.ControlPlane.Background;

internal static class HermesRunEventRules
{
    public static bool IsTerminalEvent(string eventType, AgentKind agentKind)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return false;

        return agentKind == AgentKind.DietCode
            ? IsDietCodeTerminal(eventType)
            : IsManagerTerminal(eventType);
    }

    /// <summary>DietCode runs end on run.* events only — not per assistant message.</summary>
    private static bool IsDietCodeTerminal(string eventType) =>
        eventType.Contains("run.completed", StringComparison.OrdinalIgnoreCase) ||
        eventType.Contains("run.failed", StringComparison.OrdinalIgnoreCase) ||
        eventType.Contains("run.cancelled", StringComparison.OrdinalIgnoreCase) ||
        eventType.Contains("run.stopping", StringComparison.OrdinalIgnoreCase);

    private static bool IsManagerTerminal(string eventType) =>
        IsDietCodeTerminal(eventType) ||
        eventType.Contains("message.complete", StringComparison.OrdinalIgnoreCase);

    public static bool IsActiveRunStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return status.Equals("running", StringComparison.OrdinalIgnoreCase)
            || status.Equals("active", StringComparison.OrdinalIgnoreCase)
            || status.Equals("pending", StringComparison.OrdinalIgnoreCase)
            || status.Equals("queued", StringComparison.OrdinalIgnoreCase)
            || status.Equals("processing", StringComparison.OrdinalIgnoreCase)
            || status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
            || status.Equals("waiting", StringComparison.OrdinalIgnoreCase)
            || status.Equals("awaiting_approval", StringComparison.OrdinalIgnoreCase)
            || status.Equals("requires_action", StringComparison.OrdinalIgnoreCase)
            || status.Equals("paused", StringComparison.OrdinalIgnoreCase);
    }
}
