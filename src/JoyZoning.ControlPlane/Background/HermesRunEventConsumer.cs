using System.Collections.Concurrent;
using System.Text.Json;
using JoyZoning.Agents;
using JoyZoning.Domain.Agents;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.ControlPlane.Hubs;
using JoyZoning.ControlPlane.Services;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Background;

public class HermesRunEventConsumer
{
    private readonly AgentAdapterRegistry _agents;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly ExecutorOptions _executorOptions;
    private readonly ILogger<HermesRunEventConsumer> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _activeRuns = new();
    private readonly ConcurrentDictionary<string, int> _streamResumeAttempts = new();

    public HermesRunEventConsumer(
        AgentAdapterRegistry agents,
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        IOptions<ExecutorOptions> executorOptions,
        ILogger<HermesRunEventConsumer> logger)
    {
        _agents = agents;
        _scopeFactory = scopeFactory;
        _environment = environment;
        _executorOptions = executorOptions.Value;
        _logger = logger;
    }

    public void TrackRun(string runId, AgentKind agentKind, Guid? correlationId)
    {
        if (string.IsNullOrWhiteSpace(runId))
            return;

        // Integration tests use stub adapters with empty SSE; background reconciliation
        // races DB reset and lease assertions between test methods.
        if (_environment.IsEnvironment("Testing"))
            return;

        var cts = new CancellationTokenSource();
        if (!_activeRuns.TryAdd(runId, cts))
        {
            cts.Dispose();
            return;
        }

        _ = Task.Run(() => ConsumeRunAsync(runId, agentKind, correlationId, cts.Token));
    }

    public void StopTracking(string runId)
    {
        if (_activeRuns.TryRemove(runId, out var cts))
            cts.Cancel();
    }

    /// <summary>Re-attach SSE consumer after approval resolution (not during an active consume loop).</summary>
    public void ResumeTracking(string runId, AgentKind agentKind, Guid? correlationId)
    {
        if (string.IsNullOrWhiteSpace(runId) || _environment.IsEnvironment("Testing"))
            return;

        if (_activeRuns.ContainsKey(runId))
            return;

        if (!TryReserveStreamResume(runId))
            return;

        TrackRun(runId, agentKind, correlationId);
    }

    private bool TryReserveStreamResume(string runId)
    {
        var max = Math.Max(1, _executorOptions.MaxStreamResumeAttempts);
        var count = _streamResumeAttempts.AddOrUpdate(runId, 1, static (_, c) => c + 1);
        if (count <= max)
            return true;

        _logger.LogWarning(
            "Run {RunId} exceeded max stream resume attempts ({Max}); not re-attaching SSE",
            runId,
            max);
        return false;
    }

    private async Task ConsumeRunAsync(
        string runId,
        AgentKind agentKind,
        Guid? correlationId,
        CancellationToken cancellationToken)
    {
        var sawTerminal = false;
        var streamEndAction = StreamEndAction.None;
        try
        {
            var adapter = _agents.Get(agentKind);
            await foreach (var evt in adapter.StreamEventsAsync(runId, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                using var scope = _scopeFactory.CreateScope();
                var events = scope.ServiceProvider.GetRequiredService<EventIngestor>();
                var approvals = scope.ServiceProvider.GetRequiredService<ApprovalService>();
                var executions = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<KanbanExecutionOrchestrator>();
                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OperatorHub>>();

                var source = agentKind == AgentKind.Hermes ? EventSource.Hermes : EventSource.DietCode;
                if (correlationId is { } cid && cid != Guid.Empty)
                {
                    await IngestSafeAsync(events, cid, source, evt, cancellationToken);
                    await PushUiEventsAsync(hub, agentKind, cid, evt, cancellationToken);
                    await PushTerminalOutputAsync(events, hub, cid, evt, cancellationToken);
                }

                if (IsApprovalRequest(evt.EventType))
                {
                    var (command, description) = ParseApprovalPayload(evt.PayloadJson);
                    await approvals.CreateFromHermesEventAsync(
                        runId, command, description, agentKind,
                        correlationId is { } c && c != Guid.Empty ? c : null,
                        cancellationToken);
                }

                if (HermesRunEventRules.IsTerminalEvent(evt.EventType, agentKind))
                {
                    sawTerminal = true;
                    _streamResumeAttempts.TryRemove(runId, out _);
                    await HandleTerminalEventAsync(
                        runId,
                        agentKind,
                        correlationId,
                        evt,
                        executions,
                        orchestrator,
                        events,
                        hub,
                        cancellationToken);
                    break;
                }
            }

            if (!sawTerminal && !cancellationToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                streamEndAction = await HandleStreamEndedWithoutTerminalAsync(
                    runId,
                    agentKind,
                    correlationId,
                    _agents,
                    scope.ServiceProvider.GetRequiredService<IExecutionRepository>(),
                    scope.ServiceProvider.GetRequiredService<IApprovalRepository>(),
                    scope.ServiceProvider.GetRequiredService<KanbanExecutionOrchestrator>(),
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on stop
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event stream ended for run {RunId}", runId);
            if (!sawTerminal)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    streamEndAction = await HandleStreamEndedWithoutTerminalAsync(
                        runId,
                        agentKind,
                        correlationId,
                        _agents,
                        scope.ServiceProvider.GetRequiredService<IExecutionRepository>(),
                        scope.ServiceProvider.GetRequiredService<IApprovalRepository>(),
                        scope.ServiceProvider.GetRequiredService<KanbanExecutionOrchestrator>(),
                        CancellationToken.None);
                }
                catch (Exception reconcileEx)
                {
                    _logger.LogDebug(reconcileEx, "Stream-death reconciliation failed for {RunId}", runId);
                }
            }
        }
        finally
        {
            if (_activeRuns.TryRemove(runId, out var cts))
                cts.Dispose();
        }

        // Schedule resume only after this consumer released its CTS — avoids cancelling the child immediately.
        if (streamEndAction == StreamEndAction.ResumeTracking && TryReserveStreamResume(runId))
            TrackRun(runId, agentKind, correlationId);
    }

    private static bool IsApprovalRequest(string eventType) =>
        eventType.Contains("approval.request", StringComparison.OrdinalIgnoreCase);

    private async Task<StreamEndAction> HandleStreamEndedWithoutTerminalAsync(
        string runId,
        AgentKind agentKind,
        Guid? correlationId,
        AgentAdapterRegistry agents,
        IExecutionRepository executions,
        IApprovalRepository approvals,
        KanbanExecutionOrchestrator orchestrator,
        CancellationToken cancellationToken)
    {
        if (agentKind != AgentKind.DietCode)
            return StreamEndAction.None;

        var execution = await executions.GetByRunIdAsync(runId, cancellationToken);
        if (execution is null || execution.Phase != ExecutionPhase.Running)
            return StreamEndAction.None;

        if (await approvals.HasPendingForRunAsync(runId, cancellationToken))
        {
            _logger.LogInformation(
                "SSE closed for run {RunId} with pending approval — lease stays running",
                runId);
            return StreamEndAction.ResumeTracking;
        }

        var adapter = agents.Get(agentKind);
        var pollAttempts = Math.Max(1, _executorOptions.StreamStatusPollAttempts);
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _executorOptions.StreamStatusPollIntervalSeconds));
        AgentRunPollResult? polled = null;
        for (var attempt = 0; attempt < pollAttempts; attempt++)
        {
            polled = await adapter.PollRunStatusAsync(runId, cancellationToken);
            if (polled is { IsTerminal: true })
                break;

            if (HermesRunEventRules.IsActiveRunStatus(polled?.Status))
            {
                _logger.LogDebug(
                    "SSE closed for run {RunId} but Hermes status={Status} — resuming event tracking",
                    runId,
                    polled!.Status);
                return StreamEndAction.ResumeTracking;
            }

            if (attempt < pollAttempts - 1)
                await Task.Delay(pollInterval, cancellationToken);
        }

        if (polled is { IsTerminal: true } terminal)
        {
            if (terminal.Status is "completed")
            {
                await executions.UpdatePhaseAsync(execution.Id, ExecutionPhase.Completed, cancellationToken);
                try
                {
                    await orchestrator.AgentTransitionLeaseAsync(
                        execution.WorkTaskId,
                        ExecutionLeaseStatus.Verifying,
                        reason: "Hermes run completed (polled after SSE closed)",
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Lease transition to verifying skipped for task {TaskId}", execution.WorkTaskId);
                }

                _logger.LogInformation(
                    "SSE ended early for run {RunId}; Hermes status=completed — execution completed, lease verifying",
                    runId);
                return StreamEndAction.None;
            }

            var reason = $"Hermes run ended with status '{terminal.Status}' (SSE closed early)";
            await executions.UpdatePhaseAsync(execution.Id, ExecutionPhase.Interrupted, cancellationToken);
            try
            {
                await orchestrator.RecordExecutionFailureAsync(execution.WorkTaskId, reason, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Lease failure after polled terminal for task {TaskId}", execution.WorkTaskId);
            }

            return StreamEndAction.None;
        }

        if (polled is null)
        {
            _logger.LogWarning(
                "SSE closed for run {RunId} and poll failed — resuming tracking instead of blocking lease",
                runId);
            return StreamEndAction.ResumeTracking;
        }

        _logger.LogDebug(
            "SSE closed for run {RunId} with non-terminal status {Status} — resuming tracking",
            runId,
            polled.Status);
        return StreamEndAction.ResumeTracking;
    }

    private async Task HandleTerminalEventAsync(
        string runId,
        AgentKind agentKind,
        Guid? correlationId,
        NormalizedAgentEvent evt,
        IExecutionRepository executions,
        KanbanExecutionOrchestrator orchestrator,
        EventIngestor events,
        IHubContext<OperatorHub> hub,
        CancellationToken cancellationToken)
    {
        var execution = await executions.GetByRunIdAsync(runId, cancellationToken);
        if (execution is null)
        {
            if (agentKind == AgentKind.Hermes && correlationId is { } sid && sid != Guid.Empty)
                await PushManagerCompleteAsync(hub, sid, cancellationToken);
            return;
        }

        var failed = evt.EventType.Contains("run.failed", StringComparison.OrdinalIgnoreCase);
        var cancelled = evt.EventType.Contains("run.cancelled", StringComparison.OrdinalIgnoreCase)
            || evt.EventType.Contains("run.stopping", StringComparison.OrdinalIgnoreCase);

        if (failed || cancelled)
        {
            var reason = ExtractError(evt.PayloadJson)
                ?? (cancelled ? "Hermes run cancelled" : "Hermes run failed");
            await executions.UpdatePhaseAsync(
                execution.Id,
                cancelled ? ExecutionPhase.Cancelled : ExecutionPhase.Failed,
                cancellationToken);

            try
            {
                await orchestrator.RecordExecutionFailureAsync(execution.WorkTaskId, reason, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not record execution failure for task {TaskId}", execution.WorkTaskId);
            }

            await events.IngestAsync(
                execution.WorkTaskId,
                EventSource.DietCode,
                EventTypes.DietCodeExecutionCompleted,
                new { execution.Id, runId, failed, cancelled, reason },
                cancellationToken);
            return;
        }

        await executions.UpdatePhaseAsync(execution.Id, ExecutionPhase.Completed, cancellationToken);
        await events.IngestAsync(
            execution.WorkTaskId,
            EventSource.DietCode,
            EventTypes.DietCodeExecutionCompleted,
            new { execution.Id, runId },
            cancellationToken);

        if (agentKind == AgentKind.DietCode)
        {
            try
            {
                await orchestrator.AgentTransitionLeaseAsync(
                    execution.WorkTaskId,
                    ExecutionLeaseStatus.Verifying,
                    reason: "Hermes run completed",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Lease transition to verifying skipped for task {TaskId}", execution.WorkTaskId);
            }
        }

        if (agentKind == AgentKind.Hermes && correlationId is { } sessionId && sessionId != Guid.Empty)
            await PushManagerCompleteAsync(hub, sessionId, cancellationToken);
    }

    private static async Task IngestSafeAsync(
        EventIngestor events,
        Guid correlationId,
        EventSource source,
        NormalizedAgentEvent evt,
        CancellationToken cancellationToken)
    {
        object payload;
        try
        {
            payload = JsonSerializer.Deserialize<object>(evt.PayloadJson) ?? evt.PayloadJson;
        }
        catch
        {
            payload = evt.PayloadJson;
        }

        await events.IngestAsync(correlationId, source, evt.EventType, payload, cancellationToken);
    }

    private static async Task PushManagerCompleteAsync(
        IHubContext<OperatorHub> hub,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await hub.Clients.All.SendAsync(
            "OnManagerChatComplete",
            new ManagerChatCompleteDto(sessionId),
            cancellationToken);

    private static async Task PushUiEventsAsync(
        IHubContext<OperatorHub> hub,
        AgentKind agentKind,
        Guid sessionId,
        NormalizedAgentEvent evt,
        CancellationToken cancellationToken)
    {
        if (agentKind != AgentKind.Hermes)
            return;

        if (evt.EventType.Contains("message.delta", StringComparison.OrdinalIgnoreCase))
        {
            var delta = ExtractTextDelta(evt.PayloadJson);
            if (!string.IsNullOrEmpty(delta))
            {
                await hub.Clients.All.SendAsync(
                    "OnManagerChatDelta",
                    new ManagerChatDeltaDto(sessionId, delta),
                    cancellationToken);
            }
        }
        else if (evt.EventType.Contains("message.complete", StringComparison.OrdinalIgnoreCase) ||
                 evt.EventType.Contains("run.completed", StringComparison.OrdinalIgnoreCase))
        {
            await hub.Clients.All.SendAsync(
                "OnManagerChatComplete",
                new ManagerChatCompleteDto(sessionId),
                cancellationToken);
        }
    }

    private static async Task PushTerminalOutputAsync(
        EventIngestor events,
        IHubContext<OperatorHub> hub,
        Guid correlationId,
        NormalizedAgentEvent evt,
        CancellationToken cancellationToken)
    {
        if (!evt.EventType.Contains("tool", StringComparison.OrdinalIgnoreCase) &&
            !evt.EventType.Contains("terminal", StringComparison.OrdinalIgnoreCase))
            return;

        var text = ExtractTerminalText(evt.PayloadJson);
        if (string.IsNullOrEmpty(text))
            return;

        await hub.Clients.All.SendAsync(
            "OnTerminalOutput",
            new TerminalOutputDto(correlationId, text),
            cancellationToken);

        await events.IngestAsync(
            correlationId,
            EventSource.Terminal,
            EventTypes.TerminalOutput,
            new { preview = text, agentEventType = evt.EventType },
            cancellationToken);
    }

    private static string? ExtractError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            foreach (var name in new[] { "error", "message", "detail", "output", "reason" })
            {
                if (root.TryGetProperty(name, out var prop))
                {
                    var text = prop.ValueKind == JsonValueKind.String
                        ? prop.GetString()
                        : prop.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
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
