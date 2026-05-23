using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>HTTP client to joy-bridge + bounded in-process mirror queue (fail-open).</summary>
public sealed class BroccoliQBridgeClient : IBroccoliQBridge, IHostedService, IDisposable
{
    private readonly HttpClient _http;
    private readonly BroccoliQOptions _options;
    private readonly BroccoliQRuntimeMetrics _metrics;
    private readonly IHostEnvironment _environment;
    private readonly IWebHostEnvironment _webHost;
    private readonly BroccoliQCoordinator _coordinator;
    private readonly ILogger<BroccoliQBridgeClient> _logger;
    private readonly Channel<JoyEventMirrorDto> _queue;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    public const string HttpClientName = "BroccoliQ";

    public BroccoliQBridgeClient(
        IHttpClientFactory httpClientFactory,
        IOptions<BroccoliQOptions> options,
        BroccoliQRuntimeMetrics metrics,
        IHostEnvironment environment,
        IWebHostEnvironment webHost,
        BroccoliQCoordinator coordinator,
        ILogger<BroccoliQBridgeClient> logger)
    {
        _http = httpClientFactory.CreateClient(HttpClientName);
        _options = options.Value;
        _metrics = metrics;
        _environment = environment;
        _webHost = webHost;
        _coordinator = coordinator;
        _logger = logger;
        var capacity = Math.Max(128, _options.MirrorQueueCapacity);
        _queue = Channel.CreateBounded<JoyEventMirrorDto>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public bool IsEnabled =>
        _options.Enabled && !_environment.IsEnvironment("Testing");

    public BroccoliQMirrorStats GetMirrorStats() => _metrics.Snapshot();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
            return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pumpTask = Task.Run(() => PumpMirrorQueueAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
            return;

        _queue.Writer.TryComplete();
        _cts.Cancel();
        if (_pumpTask is not null)
        {
            try
            {
                await _pumpTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogDebug("BroccoliQ mirror pump did not stop within 3s");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // shutdown
            }
        }

        if (IsEnabled)
        {
            try
            {
                await FlushBridgeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "BroccoliQ bridge flush on shutdown skipped");
            }
        }

        _cts.Dispose();
    }

    public void Dispose() => _cts?.Dispose();

    public void EnqueueJoyEvent(JoyEvent joyEvent)
    {
        if (!IsEnabled)
            return;

        var dto = ToDto(joyEvent);
        if (_queue.Writer.TryWrite(dto))
        {
            _metrics.RecordEnqueued();
            _metrics.SetQueueDepth(_queue.Reader.Count);
            return;
        }

        _metrics.RecordDropped();
        _metrics.SetQueueDepth(_queue.Reader.Count);
        _logger.LogDebug(
            "BroccoliQ mirror queue full ({Capacity}); dropped joy_event {Id}",
            _options.MirrorQueueCapacity,
            joyEvent.Id);
    }

    public async Task<BroccoliQHealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var built = BroccoliQPaths.IsDistBuilt(BroccoliQPaths.ResolveRepoRoot(_options, _webHost.ContentRootPath));
        if (!IsEnabled)
        {
            return new BroccoliQHealthReport(
                HealthState.Unavailable,
                "BroccoliQ integration disabled",
                Enabled: false,
                WorkerAutoStart: _options.AutoStartWorker,
                _options.DatabasePath,
                BridgeBuilt: built);
        }

        if (!built)
        {
            return new BroccoliQHealthReport(
                HealthState.Unavailable,
                "BroccoliQ dist not built (run scripts/broccoliq-build.sh)",
                Enabled: true,
                WorkerAutoStart: _options.AutoStartWorker,
                _options.DatabasePath,
                BridgeBuilt: false);
        }

        try
        {
            using var response = await _http.GetAsync("health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new BroccoliQHealthReport(
                    HealthState.Unavailable,
                    $"Bridge HTTP {(int)response.StatusCode}",
                    Enabled: true,
                    WorkerAutoStart: _options.AutoStartWorker,
                    _options.DatabasePath,
                    BridgeBuilt: true);
            }

            var body = await response.Content.ReadFromJsonAsync<BridgeHealthDto>(cancellationToken);
            _coordinator.SetBridgeReady(true);
            return new BroccoliQHealthReport(
                HealthState.Healthy,
                body?.Service ?? "broccoliq-joy-bridge",
                Enabled: true,
                WorkerAutoStart: _options.AutoStartWorker,
                body?.DbPath ?? _options.DatabasePath,
                BridgeBuilt: true,
                BridgeMirrorCount: body?.MirrorCount);
        }
        catch (Exception ex)
        {
            return new BroccoliQHealthReport(
                HealthState.Unavailable,
                ex.Message,
                Enabled: true,
                WorkerAutoStart: _options.AutoStartWorker,
                _options.DatabasePath,
                BridgeBuilt: built);
        }
    }

    public async Task<int> BackfillJoyEventsAsync(
        IReadOnlyList<JoyEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || events.Count == 0)
            return 0;

        var dtos = events.Select(ToDto).ToList();
        var sent = 0;
        for (var i = 0; i < dtos.Count; i += _options.MirrorBatchSize)
        {
            var slice = dtos.Skip(i).Take(_options.MirrorBatchSize).ToList();
            if (await PostEventBatchAsync(slice, cancellationToken))
                sent += slice.Count;
        }

        if (sent > 0)
            _metrics.RecordDelivered(sent);

        return sent;
    }

    public async Task FlushBridgeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return;

        try
        {
            using var response = await _http.PostAsync("v1/flush", null, cancellationToken);
            if (!response.IsSuccessStatusCode)
                _metrics.RecordFailed($"flush HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.RecordFailed(ex.Message);
        }
    }

    public async Task MirrorWorkTaskAsync(
        Guid taskId,
        string title,
        string? description,
        string status,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return;

        var dto = new WorkTaskMirrorDto(
            taskId,
            title,
            description,
            status,
            priority,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        try
        {
            using var response = await _http.PostAsJsonAsync("v1/work-tasks", dto, cancellationToken);
            if (response.IsSuccessStatusCode)
                _metrics.RecordTasksMirrored();
            else
                _metrics.RecordFailed($"work-task HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.RecordFailed(ex.Message);
            _logger.LogDebug(ex, "BroccoliQ work-task mirror skipped");
        }
    }

    private async Task PumpMirrorQueueAsync(CancellationToken cancellationToken)
    {
        var batch = new List<JoyEventMirrorDto>(Math.Max(8, _options.MirrorBatchSize));
        var flushInterval = TimeSpan.FromMilliseconds(Math.Max(10, _options.MirrorFlushIntervalMs));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();
                if (await _queue.Reader.WaitToReadAsync(cancellationToken))
                {
                    var deadline = DateTimeOffset.UtcNow + flushInterval;
                    while (batch.Count < _options.MirrorBatchSize &&
                           DateTimeOffset.UtcNow < deadline &&
                           _queue.Reader.TryRead(out var item))
                    {
                        batch.Add(item);
                    }

                    if (batch.Count == 0 && _queue.Reader.TryRead(out var solo))
                        batch.Add(solo);
                }

                _metrics.SetQueueDepth(_queue.Reader.Count);

                if (batch.Count > 0)
                    await FlushBatchAsync(batch, cancellationToken);
                else
                    await Task.Delay(flushInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _metrics.RecordFailed(ex.Message);
                _logger.LogWarning(ex, "BroccoliQ mirror pump error");
                await Task.Delay(500, cancellationToken);
            }
        }

        while (_queue.Reader.TryRead(out var remaining))
        {
            batch.Add(remaining);
            if (batch.Count >= _options.MirrorBatchSize)
            {
                await FlushBatchAsync(batch, CancellationToken.None);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            await FlushBatchAsync(batch, CancellationToken.None);
    }

    private async Task FlushBatchAsync(List<JoyEventMirrorDto> batch, CancellationToken cancellationToken)
    {
        if (!_coordinator.BridgeReady)
        {
            var health = await GetHealthAsync(cancellationToken);
            if (health.State == HealthState.Healthy)
                _coordinator.SetBridgeReady(true);
        }

        var attempts = Math.Max(1, _options.MirrorPumpRetryAttempts);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (await PostEventBatchAsync(batch, cancellationToken))
                {
                    _metrics.RecordDelivered(batch.Count);
                    return;
                }

                _metrics.RecordFailed($"joy-events HTTP attempt {attempt}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _metrics.RecordFailed(ex.Message);
                _logger.LogDebug(ex, "BroccoliQ batch mirror attempt {Attempt}/{Max}", attempt, attempts);
            }

            if (attempt < attempts)
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
        }

        _metrics.SetQueueDepth(_queue.Reader.Count);
    }

    private async Task<bool> PostEventBatchAsync(
        IReadOnlyList<JoyEventMirrorDto> batch,
        CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            batch.Count == 1 ? "v1/joy-events" : "v1/joy-events/batch",
            batch.Count == 1 ? (object)batch[0] : new { events = batch },
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static JoyEventMirrorDto ToDto(JoyEvent joyEvent) => new(
        joyEvent.Id,
        joyEvent.CorrelationId,
        joyEvent.Source.ToString(),
        joyEvent.Type,
        joyEvent.PayloadJson,
        joyEvent.OccurredAt);

    internal sealed record JoyEventMirrorDto(
        long Id,
        Guid CorrelationId,
        string Source,
        string Type,
        string PayloadJson,
        DateTimeOffset OccurredAt);

    private sealed record WorkTaskMirrorDto(
        Guid TaskId,
        string Title,
        string? Description,
        string Status,
        int Priority,
        long UpdatedAt);

    private sealed record BridgeHealthDto(
        string? Status,
        string? Service,
        [property: JsonPropertyName("dbPath")] string? DbPath,
        [property: JsonPropertyName("mirrorCount")] int? MirrorCount);
}
