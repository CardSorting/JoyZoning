using System.Diagnostics;
using System.Text.Json;

namespace JoyZoning.Cli;

public static class DoctorCommand
{
    public static async Task<int> RunAsync(CliContext ctx)
    {
        var checks = new List<object>();
        var ok = true;

        void Add(string id, string status, string detail)
        {
            checks.Add(new { id, status, detail });
            if (status is "fail" or "warn")
                ok = false;
        }

        if (!HasDotNetSdk())
            Add("dotnet_sdk", "fail", ".NET 8 SDK not found on PATH. Install from https://dot.net");
        else
            Add("dotnet_sdk", "ok", "dotnet SDK available");

        using var client = new JoyZoningCliClient(ctx.BaseUrl, TimeSpan.FromSeconds(5));
        var health = await client.HealthAsync();
        if (health.IsSuccess)
            Add("control_plane", "ok", $"Reachable at {ctx.BaseUrl}");
        else
            Add("control_plane", "fail",
                health.IsNetworkError
                    ? $"Cannot reach {ctx.BaseUrl}. Start: dotnet run --project src/JoyZoning.ControlPlane"
                    : health.Message ?? "health check failed");

        if (health.IsSuccess)
        {
            var hermes = await client.HermesHealthAsync();
            Add("hermes_api", hermes.IsSuccess ? "ok" : "warn", hermes.Message ?? hermes.RawText);
        }

        if (!ctx.Quiet)
        {
            var envelope = new { ok, checks };
            return CliOutput.WriteEnvelope(ctx, envelope);
        }

        return ok ? 0 : 1;
    }

    private static bool HasDotNetSdk()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
