using System.Diagnostics;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Starts and supervises the Node joy-bridge worker beside the control plane.</summary>
public sealed class BroccoliQProcessService : IDisposable
{
    private readonly IBroccoliQBridge _bridge;
    private readonly BroccoliQCoordinator _coordinator;
    private readonly BroccoliQOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly IWebHostEnvironment _webHost;
    private readonly ILogger<BroccoliQProcessService> _logger;
    private readonly object _lock = new();
    private Process? _workerProcess;
    private string? _repoRoot;

    public BroccoliQProcessService(
        IBroccoliQBridge bridge,
        BroccoliQCoordinator coordinator,
        IOptions<BroccoliQOptions> options,
        IHostEnvironment environment,
        IWebHostEnvironment webHost,
        ILogger<BroccoliQProcessService> logger,
        IHostApplicationLifetime? lifetime = null)
    {
        _bridge = bridge;
        _coordinator = coordinator;
        _options = options.Value;
        _environment = environment;
        _webHost = webHost;
        _logger = logger;
        lifetime?.ApplicationStopping.Register(StopWorker);
    }

    public void Dispose() => StopWorker();

    public bool IsManagedWorkerRunning
    {
        get
        {
            lock (_lock)
                return _workerProcess is { HasExited: false };
        }
    }

    public async Task<bool> EnsureWorkerRunningAsync(CancellationToken cancellationToken = default)
    {
        if (!_bridge.IsEnabled || !_options.AutoStartWorker || _environment.IsEnvironment("Testing"))
            return false;

        _repoRoot ??= BroccoliQPaths.ResolveRepoRoot(_options, _webHost.ContentRootPath);
        if (!BroccoliQPaths.IsDistBuilt(_repoRoot))
        {
            _logger.LogWarning(
                "BroccoliQ dist missing at {Root}/broccoliq/dist — run scripts/broccoliq-build.sh",
                _repoRoot ?? "(unknown repo root)");
            return false;
        }

        BroccoliQPaths.EnsureDatabaseDirectory(_options.DatabasePath);

        var health = await _bridge.GetHealthAsync(cancellationToken);
        if (health.State == HealthState.Healthy)
            return true;

        if (!TryStartWorkerProcess())
            return false;

        var deadline = DateTimeOffset.UtcNow.AddSeconds(_options.WorkerStartupTimeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(250, cancellationToken);
            health = await _bridge.GetHealthAsync(cancellationToken);
            if (health.State == HealthState.Healthy)
            {
                _coordinator.SetBridgeReady(true);
                _logger.LogInformation("BroccoliQ joy-bridge ready at {Url}", _options.BridgeListenUrl);
                return true;
            }
        }

        _logger.LogWarning(
            "BroccoliQ joy-bridge did not become healthy within {Seconds}s ({Url})",
            _options.WorkerStartupTimeoutSeconds,
            _options.BridgeListenUrl);
        return false;
    }

    private bool TryStartWorkerProcess()
    {
        lock (_lock)
        {
            if (_workerProcess is { HasExited: false })
                return true;

            if (_workerProcess is { HasExited: true })
            {
                try { _workerProcess.Dispose(); } catch { /* ignore */ }
                _workerProcess = null;
            }

            _repoRoot ??= BroccoliQPaths.ResolveRepoRoot(_options, _webHost.ContentRootPath);
            var script = BroccoliQPaths.ResolveJoyBridgeScript(_repoRoot);
            if (script is null || !File.Exists(script))
            {
                _logger.LogWarning(
                    "BroccoliQ joy-bridge script not found. Set BroccoliQ:RepoRoot or JOYZONING_REPO_ROOT.");
                return false;
            }

            if (!BroccoliQPaths.IsDistBuilt(_repoRoot))
                return false;

            var runtime = ResolveNodeRuntime();
            if (runtime is null)
            {
                _logger.LogWarning("Node.js or Bun runtime not found for BroccoliQ joy-bridge");
                return false;
            }

            var uri = new Uri(_options.BridgeListenUrl);
            if (!uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "BroccoliQ BridgeListenUrl must be localhost (got {Host}); refusing to spawn worker",
                    uri.Host);
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = runtime,
                Arguments = $"\"{script}\"",
                WorkingDirectory = Path.GetDirectoryName(script)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (!string.IsNullOrWhiteSpace(_options.DatabasePath))
                psi.Environment["BROCCOLIQ_DB_PATH"] = _options.DatabasePath;
            psi.Environment["BROCCOLIQ_BRIDGE_HOST"] = uri.Host;
            psi.Environment["BROCCOLIQ_BRIDGE_PORT"] = uri.Port.ToString();
            psi.Environment["BROCCOLIQ_MAX_BODY_BYTES"] = Math.Max(64_000, _options.MaxRequestBodyBytes).ToString();
            psi.Environment["JOYZONING_REPO_ROOT"] = _repoRoot ?? "";

            _workerProcess = Process.Start(psi);
            if (_workerProcess is null)
            {
                _logger.LogWarning("Failed to start BroccoliQ joy-bridge process");
                return false;
            }

            _workerProcess.EnableRaisingEvents = true;
            _workerProcess.Exited += OnWorkerExited;

            _workerProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    _logger.LogDebug("[broccoliq] {Line}", e.Data);
            };
            _workerProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    _logger.LogDebug("[broccoliq:err] {Line}", e.Data);
            };
            _workerProcess.BeginOutputReadLine();
            _workerProcess.BeginErrorReadLine();

            _logger.LogInformation(
                "Started BroccoliQ joy-bridge pid={Pid} runtime={Runtime}",
                _workerProcess.Id,
                runtime);
            return true;
        }
    }

    private void OnWorkerExited(object? sender, EventArgs e)
    {
        int? code = null;
        lock (_lock)
        {
            if (_workerProcess is not null)
                code = _workerProcess.ExitCode;
        }

        _coordinator.SetBridgeReady(false);
        _logger.LogWarning(
            "BroccoliQ joy-bridge exited (code {Code}); supervisor will restart if enabled",
            code);
    }

    private void StopWorker()
    {
        lock (_lock)
        {
            if (_workerProcess is null)
                return;

            if (_workerProcess.HasExited)
            {
                try { _workerProcess.Dispose(); } catch { /* ignore */ }
                _workerProcess = null;
                return;
            }

            try
            {
                _workerProcess.Exited -= OnWorkerExited;
                _workerProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to stop BroccoliQ joy-bridge on shutdown");
            }
            finally
            {
                _workerProcess.Dispose();
                _workerProcess = null;
            }
        }
    }

    private static string? ResolveNodeRuntime()
    {
        foreach (var name in new[] { "node", "bun" })
        {
            try
            {
                var psi = new ProcessStartInfo(name, "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                if (p is null) continue;
                p.WaitForExit(3000);
                if (p.ExitCode == 0)
                    return name;
            }
            catch
            {
                // try next runtime
            }
        }

        return null;
    }
}
