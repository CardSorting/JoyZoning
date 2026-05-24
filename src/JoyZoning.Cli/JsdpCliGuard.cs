using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;

namespace JoyZoning.Cli;

public static class JsdpCliGuard
{
    public static async Task EnsureNotJsdpSessionAsync(JoyZoningCliClient client, Guid sessionId)
    {
        if (await IsJsdpEnforcedSessionAsync(client, sessionId))
            throw new CliUsageException(JsdpSessionPolicy.PlanRunSkipReason);
    }

    public static async Task EnsureNotJsdpTaskAsync(JoyZoningCliClient client, Guid taskId)
    {
        var task = await client.GetTaskAsync(taskId);
        if (!task.IsSuccess || task.Body is null)
            return;

        if (!task.Body.Value.TryGetProperty("operatorSessionId", out var sidProp))
            return;

        if (!Guid.TryParse(sidProp.GetString(), out var sessionId))
            return;

        await EnsureNotJsdpSessionAsync(client, sessionId);
    }

    public static async Task<bool> IsJsdpEnforcedSessionAsync(JoyZoningCliClient client, Guid sessionId)
    {
        var session = await client.GetSessionAsync(sessionId);
        if (!session.IsSuccess || session.Body is null)
            return false;

        return ReadExecutionMode(session.Body.Value) == SessionExecutionMode.BoundedRole;
    }

    public static SessionExecutionMode? ReadExecutionMode(JsonElement session)
    {
        if (!session.TryGetProperty("executionMode", out var modeProp))
            return null;

        if (modeProp.ValueKind == JsonValueKind.String
            && Enum.TryParse<SessionExecutionMode>(modeProp.GetString(), ignoreCase: true, out var parsed))
            return parsed;

        if (modeProp.ValueKind == JsonValueKind.Number)
            return (SessionExecutionMode)modeProp.GetInt32();

        return null;
    }
}
