using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Events;
using JoyZoning.Persistence.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using JoyZoning.ControlPlane.Hubs;

namespace JoyZoning.ControlPlane.Services;

public class EventIngestor
{
    private readonly IEventRepository _events;
    private readonly IHubContext<OperatorHub> _hub;
    private readonly IBroccoliQBridge _broccoliQ;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EventIngestor> _logger;

    public EventIngestor(
        IEventRepository events,
        IHubContext<OperatorHub> hub,
        IBroccoliQBridge broccoliQ,
        IHostEnvironment environment,
        ILogger<EventIngestor> logger)
    {
        _events = events;
        _hub = hub;
        _broccoliQ = broccoliQ;
        _environment = environment;
        _logger = logger;
    }

    public async Task<JoyEvent> IngestAsync(
        Guid correlationId,
        EventSource source,
        string type,
        object? payload = null,
        CancellationToken cancellationToken = default)
    {
        var json = payload is null ? "{}" : JsonSerializer.Serialize(payload);
        var evt = await _events.AppendAsync(correlationId, source, type, json, cancellationToken);

        if (!_environment.IsEnvironment("Testing"))
        {
            try
            {
                await _hub.Clients.All.SendAsync(
                    "OnJoyEvent",
                    new JoyEventDto(evt.Id, evt.CorrelationId, evt.Source.ToString(), evt.Type, evt.PayloadJson, evt.OccurredAt),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SignalR OnJoyEvent broadcast skipped");
            }
        }

        if (_broccoliQ.IsEnabled)
            _broccoliQ.EnqueueJoyEvent(evt);

        return evt;
    }
}

public record JoyEventDto(long Id, Guid CorrelationId, string Source, string Type, string PayloadJson, DateTimeOffset OccurredAt);
