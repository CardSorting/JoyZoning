using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class YoloLeaseJson
{
    public static bool TryReadStatus(JsonElement lease, out ExecutionLeaseStatus status)
    {
        status = ExecutionLeaseStatus.Leased;
        if (!lease.TryGetProperty("status", out var prop))
            return false;
        status = (ExecutionLeaseStatus)prop.GetInt32();
        return true;
    }

    public static bool IsActiveStatus(ExecutionLeaseStatus status) =>
        KanbanExecutionRules.ActiveLeaseStatuses.Contains(status);

    public static bool IsBlockedRecoverable(ExecutionLeaseStatus status, YoloPolicy policy) =>
        policy.AllowRecover && status == ExecutionLeaseStatus.Blocked;

    public static HandoffPacket? TryReadHandoff(JsonElement lease)
    {
        if (!lease.TryGetProperty("handoffPacketJson", out var prop)
            || prop.ValueKind != JsonValueKind.String)
            return null;

        var json = prop.GetString();
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return HandoffPacketBuilder.Deserialize(json);
        }
        catch
        {
            return null;
        }
    }

    public static Guid? ReadGuid(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && Guid.TryParse(p.GetString(), out var g) ? g : null;

    public static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
