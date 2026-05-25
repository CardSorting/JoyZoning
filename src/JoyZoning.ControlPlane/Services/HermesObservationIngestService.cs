using System.Text.Json;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Microsoft.Extensions.Logging;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Ingests non-authoritative Hermes runtime observations for habitat supervision.
/// Canonical operational state remains on Hermes (~/.hermes/joyzoning/journal.db).
/// </summary>
public class HermesObservationIngestService
{
    private readonly EventIngestor _ingestor;
    private readonly HermesObservationReadModel _readModel;
    private readonly HermesObservationDedupe _dedupe;
    private readonly HermesObservationRateLimiter _rateLimiter;
    private readonly ILogger<HermesObservationIngestService> _logger;

    public HermesObservationIngestService(
        EventIngestor ingestor,
        HermesObservationReadModel readModel,
        HermesObservationDedupe dedupe,
        HermesObservationRateLimiter rateLimiter,
        ILogger<HermesObservationIngestService> logger)
    {
        _ingestor = ingestor;
        _readModel = readModel;
        _dedupe = dedupe;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<HermesObservationIngestResult> IngestAsync(
        HermesObservationPayload payload,
        string? rateLimitKey = null,
        CancellationToken cancellationToken = default)
    {
        if (HabitatLayerGuardrails.RejectsAuthoritativeObservation(payload.Authoritative))
        {
            return HermesObservationIngestResult.Rejected(
                "authoritative_forbidden",
                "Habitat cannot accept authoritative runtime state. Hermes journal remains canonical.");
        }

        if (!string.Equals(payload.Source, HabitatLayerGuardrails.HermesRuntimeSource, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(payload.Source, "Hermes", StringComparison.OrdinalIgnoreCase))
        {
            return HermesObservationIngestResult.Rejected(
                "invalid_source",
                "Observation source must be hermes-runtime.");
        }

        if (!Guid.TryParse(payload.ScopeId, out var correlationId))
        {
            correlationId = Guid.TryParse(payload.SessionId, out var sid) ? sid : Guid.Empty;
            if (correlationId == Guid.Empty)
                return HermesObservationIngestResult.Rejected("invalid_scope", "scopeId or sessionId must be a GUID.");
        }

        var rlKey = rateLimitKey ?? correlationId.ToString();
        if (!_rateLimiter.Allow(rlKey, out var rateReason))
        {
            return HermesObservationIngestResult.Rejected(
                rateReason ?? "rate_limited",
                "Observation ingest rate limit exceeded.");
        }

        var dedupeKey = BuildDedupeKey(payload);
        if (!_dedupe.TryAccept(dedupeKey, out var dupReason))
        {
            return HermesObservationIngestResult.AcceptedDuplicate(correlationId);
        }

        var stateHint = payload.Payload?.TryGetProperty("state", out var st) == true && st.ValueKind == JsonValueKind.String
            ? st.GetString()
            : null;

        var ingestPayload = new
        {
            layer = payload.Layer,
            scopeId = payload.ScopeId,
            sessionId = payload.SessionId,
            runId = payload.RunId,
            timestamp = payload.Timestamp,
            source = payload.Source,
            authoritative = false,
            data = payload.Payload,
        };

        var evt = await _ingestor.IngestAsync(
            correlationId,
            EventSource.Hermes,
            payload.Type,
            ingestPayload,
            cancellationToken);

        _readModel.ApplyObservation(new HermesObservationSnapshot(
            payload.Type,
            payload.Layer,
            payload.ScopeId,
            payload.SessionId,
            payload.RunId,
            stateHint,
            DateTimeOffset.FromUnixTimeMilliseconds((long)(payload.Timestamp * 1000))));

        return HermesObservationIngestResult.Accepted(evt.Id, correlationId);
    }

    private static string BuildDedupeKey(HermesObservationPayload payload)
    {
        var bucket = (long)Math.Floor(payload.Timestamp * 10);
        return $"{payload.Type}|{payload.ScopeId}|{payload.RunId}|{bucket}";
    }
}

public record HermesObservationPayload(
    string Type,
    string Layer,
    string? ScopeId,
    string? SessionId,
    string? RunId,
    JsonElement? Payload,
    double Timestamp,
    string Source,
    bool Authoritative);

public record HermesObservationIngestResult(
    bool Ok,
    string? Error,
    string? Message,
    long? EventId = null,
    Guid? CorrelationId = null,
    bool Duplicate = false)
{
    public static HermesObservationIngestResult Accepted(long eventId, Guid correlationId) =>
        new(true, null, null, eventId, correlationId);

    public static HermesObservationIngestResult Rejected(string error, string message) =>
        new(false, error, message);

    public static HermesObservationIngestResult AcceptedDuplicate(Guid correlationId) =>
        new(true, null, null, null, correlationId, Duplicate: true);
}
