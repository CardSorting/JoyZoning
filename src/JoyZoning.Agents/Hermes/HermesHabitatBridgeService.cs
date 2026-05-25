using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JoyZoning.Domain.Configuration;

namespace JoyZoning.Agents.Hermes;

/// <summary>
/// Notifies Hermes runtime journal after habitat operator accept-merge (CONVERGED transition).
/// </summary>
public class HermesHabitatBridgeService
{
    private readonly HermesRuntimeSettings _runtime;
    private readonly ControlPlaneOptions _controlPlane;
    private readonly ILogger<HermesHabitatBridgeService> _logger;

    public HermesHabitatBridgeService(
        HermesRuntimeSettings runtime,
        IOptions<ControlPlaneOptions> controlPlane,
        ILogger<HermesHabitatBridgeService> logger)
    {
        _runtime = runtime;
        _controlPlane = controlPlane.Value;
        _logger = logger;
    }

    public async Task<HabitatBridgeResult> NotifyMergeAcceptedAsync(
        Guid taskId,
        string? kanbanTaskId = null,
        string? hermesSessionId = null,
        string? summary = null,
        CancellationToken cancellationToken = default)
    {
        var installRoot = _runtime.GetSnapshot().InstallRoot;
        if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot))
        {
            return HabitatBridgeResult.FromSkip("Hermes InstallRoot not configured.");
        }

        var script = Path.Combine(installRoot, "scripts", "joyzoning_habitat_ack.py");
        if (!File.Exists(script))
            return HabitatBridgeResult.FromSkip($"Bridge script not found: {script}");

        var python = ResolvePython(installRoot);
        if (python is null)
            return HabitatBridgeResult.FromSkip("Python venv not found under Hermes InstallRoot.");

        var token = Environment.GetEnvironmentVariable("JOYZONING_HABITAT_BRIDGE_TOKEN") ?? "";
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(_controlPlane.InternalToken))
            token = _controlPlane.InternalToken;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = python,
                WorkingDirectory = installRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add("--scope");
            psi.ArgumentList.Add(taskId.ToString());
            if (!string.IsNullOrWhiteSpace(kanbanTaskId))
            {
                psi.ArgumentList.Add("--kanban-task");
                psi.ArgumentList.Add(kanbanTaskId.Trim());
            }
            if (!string.IsNullOrWhiteSpace(hermesSessionId))
            {
                psi.ArgumentList.Add("--hermes-session");
                psi.ArgumentList.Add(hermesSessionId.Trim());
            }
            if (!string.IsNullOrWhiteSpace(token))
            {
                psi.ArgumentList.Add("--token");
                psi.ArgumentList.Add(token);
            }
            if (!string.IsNullOrWhiteSpace(summary))
            {
                psi.ArgumentList.Add("--summary");
                psi.ArgumentList.Add(summary);
            }
            using var proc = Process.Start(psi);
            if (proc is null)
                return HabitatBridgeResult.FromFailure("Failed to start habitat bridge process.");

            var stdout = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await proc.StandardError.ReadToEndAsync(cancellationToken);
            await proc.WaitForExitAsync(cancellationToken);

            if (proc.ExitCode != 0)
            {
                _logger.LogWarning(
                    "Hermes habitat bridge failed for {TaskId}: {Stderr}",
                    taskId,
                    string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
                return HabitatBridgeResult.FromFailure(stderr.Trim().Length > 0 ? stderr : stdout);
            }

            try
            {
                using var doc = JsonDocument.Parse(stdout);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True
                    && ok.GetBoolean())
                {
                    var state = root.TryGetProperty("state", out var st) ? st.GetString() : "converged";
                    return HabitatBridgeResult.FromSuccess(state ?? "converged");
                }

                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : stdout;
                return HabitatBridgeResult.FromFailure(msg ?? "Bridge returned success=false");
            }
            catch (JsonException)
            {
                return HabitatBridgeResult.FromSuccess("converged");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Hermes habitat bridge exception for {TaskId}", taskId);
            return HabitatBridgeResult.FromFailure(ex.Message);
        }
    }

    private static string? ResolvePython(string installRoot)
    {
        foreach (var venv in new[] { ".venv", "venv" })
        {
            var py = Path.Combine(installRoot, venv, "bin", "python3");
            if (File.Exists(py))
                return py;
            py = Path.Combine(installRoot, venv, "bin", "python");
            if (File.Exists(py))
                return py;
        }
        return null;
    }
}

public record HabitatBridgeResult(bool Succeeded, bool WasSkipped, string? State, string? Message)
{
    public static HabitatBridgeResult FromSuccess(string state) => new(true, false, state, null);
    public static HabitatBridgeResult FromSkip(string message) => new(false, true, null, message);
    public static HabitatBridgeResult FromFailure(string message) => new(false, false, null, message);
}
