using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace JoyZoning.Agents.Hermes;

/// <summary>Starts Hermes child processes without blocking on redirected pipe buffers.</summary>
internal static class HermesChildProcessHost
{
    public static Process? Start(ProcessStartInfo psi, ILogger? logger, string label)
    {
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        var process = Process.Start(psi);
        if (process is null)
            return null;

        _ = DrainAsync(process.StandardOutput, $"{label}:stdout", logger);
        _ = DrainAsync(process.StandardError, $"{label}:stderr", logger);
        return process;
    }

    private static async Task DrainAsync(StreamReader reader, string channel, ILogger? logger)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
                logger?.LogDebug("{Channel} {Line}", channel, line);
        }
        catch
        {
            // process exited
        }
    }
}
