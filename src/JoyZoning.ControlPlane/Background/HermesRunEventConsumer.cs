using System.Collections.Concurrent;
using System.Text.Json;
using JoyZoning.Agents;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace JoyZoning.ControlPlane.Background;

public class HermesRunEventConsumer
{
    private readonly AgentAdapterRegistry _agents;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HermesRunEventConsumer> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeRuns = new();

    public HermesRunEventConsumer(
        AgentAdapterRegistry agents,
        IServiceScopeFactory scopeFactory,
        ILogger<HermesRunEventConsumer> logger)
    {
        _agents = agents;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void TrackRun(string runId, AgentKind agentKind, Guid? correlationId)
    {
        if (_activeRuns.ContainsKey(runId)) return;

        var cts = new CancellationTokenSource();
        _activeRuns[runId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                var adapter = _agents.Get(agentKind);
                await foreach (var evt in adapter.StreamEventsAsync(runId, cts.Token))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var events = scope.ServiceProvider.GetRequiredService<EventIngestor>();
                    var approvals = scope.ServiceProvider.GetRequiredService<ApprovalService>();
                    var executions = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();

                    var source = agentKind == AgentKind.Hermes ? EventSource.Hermes : EventSource.DietCode;
                    var correlation = correlationId ?? Guid.Empty;

                    await events.IngestAsync(correlation, source, evt.EventType,
                        JsonSerializer.Deserialize<object>(evt.PayloadJson) ?? evt.PayloadJson,
                        cts.Token);

                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OperatorHub>>();
                    await PushUiEventsAsync(hub, agentKind, correlationId, evt, cts.Token);
                    await PushTerminalOutputAsync(hub, correlationId, evt, cts.Token);

                    if (evt.EventType.Contains("approval.request", StringComparison.OrdinalIgnoreCase))
                    {
                        var (command, description) = ParseApprovalPayload(evt.PayloadJson);
                        await approvals.CreateFromHermesEventAsync(
                            runId, command, description, agentKind,
                            correlationId != Guid.Empty ? correlationId : null,
                            cts.Token);
                    }

                    if (evt.EventType.Contains("run.completed", StringComparison.OrdinalIgnoreCase) ||
                        evt.EventType.Contains("message.complete", StringComparison.OrdinalIgnoreCase))
                    {
                        var execution = await executions.GetByRunIdAsync(runId, cts.Token);
                        if (execution is not null)
                        {
                            await executions.UpdatePhaseAsync(
                                execution.Id, ExecutionPhase.Completed, cts.Token);
                            await events.IngestAsync(
                                execution.WorkTaskId,
                                EventSource.DietCode,
                                EventTypes.DietCodeExecutionCompleted,
                                new { execution.Id, runId },
                                cts.Token);
                        }
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event stream ended for run {RunId}", runId);
            }
            finally
            {
                _activeRuns.TryRemove(runId, out _);
            }
        });
    }

    public void StopTracking(string runId)
    {
        if (_activeRuns.TryRemove(runId, out var cts))
            cts.Cancel();
    }

    private static async Task PushUiEventsAsync(
        IHubContext<OperatorHub> hub,
        AgentKind agentKind,
        Guid? sessionId,
        NormalizedAgentEvent evt,
        CancellationToken cancellationToken)
    {
        if (agentKind != AgentKind.Hermes || sessionId is null || sessionId == Guid.Empty)
            return;

        if (evt.EventType.Contains("message.delta", StringComparison.OrdinalIgnoreCase))
        {
            var delta = ExtractTextDelta(evt.PayloadJson);
            if (!string.IsNullOrEmpty(delta))
            {
                await hub.Clients.All.SendAsync(
                    "OnManagerChatDelta",
                    new ManagerChatDeltaDto(sessionId.Value, delta), // sessionId validated above
                    cancellationToken);
            }
        }
        else if (evt.EventType.Contains("message.complete", StringComparison.OrdinalIgnoreCase))
        {
            await hub.Clients.All.SendAsync(
                "OnManagerChatComplete",
                new ManagerChatCompleteDto(sessionId.Value),
                cancellationToken);
        }
    }

    private static async Task PushTerminalOutputAsync(
        IHubContext<OperatorHub> hub,
        Guid? correlationId,
        NormalizedAgentEvent evt,
        CancellationToken cancellationToken)
    {
        if (correlationId is null || correlationId == Guid.Empty)
            return;

        if (!evt.EventType.Contains("tool", StringComparison.OrdinalIgnoreCase) &&
            !evt.EventType.Contains("terminal", StringComparison.OrdinalIgnoreCase))
            return;

        var text = ExtractTerminalText(evt.PayloadJson);
        if (string.IsNullOrEmpty(text))
            return;

        await hub.Clients.All.SendAsync(
            "OnTerminalOutput",
            new TerminalOutputDto(correlationId.Value, text),
            cancellationToken);

    }

    private static string ExtractTerminalText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("preview", out var preview))
                return preview.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("output", out var output))
                return output.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("result", out var result))
                return result.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("stdout", out var stdout))
                return stdout.GetString() ?? "";
        }
        catch
        {
            // ignore
        }
        return "";
    }

    private static string ExtractTextDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("delta", out var d))
                return d.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("content", out var c))
                return c.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("text", out var t))
                return t.GetString() ?? "";
        }
        catch
        {
            // ignore
        }
        return "";
    }

    private static (string Command, string Description) ParseApprovalPayload(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var command = doc.RootElement.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
            var description = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() ?? "" : json;
            return (command, description);
        }
        catch
        {
            return ("", json);
        }
    }
}
