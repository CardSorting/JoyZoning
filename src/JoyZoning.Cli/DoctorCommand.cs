using System.Diagnostics;
using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Orchestration;

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

        var root = AgentOperationsCommand.FindWorkspaceRoot();
        var localDoctor = AgentOperationsCommand.BuildLocalDoctor(root);
        foreach (var check in localDoctor.Checks)
            Add(check.Id, check.Status, check.Detail);

        if (localDoctor.Ok)
            AgentOperationsCommand.TryWriteManifestCache(root, AgentOperationsManifest.BuildStatic());

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

            var broccoliq = await client.BroccoliQHealthAsync();
            if (broccoliq.IsSuccess && broccoliq.Body.HasValue)
            {
                var body = broccoliq.Body.Value;
                var enabled = !body.TryGetProperty("enabled", out var en) || en.GetBoolean();
                if (!enabled)
                {
                    Add("broccoliq_bridge", "ok", "BroccoliQ integration disabled in config");
                }
                else
                {
                    var built = !body.TryGetProperty("bridgeBuilt", out var bb) || bb.GetBoolean();
                    var bridgeOk = body.TryGetProperty("status", out var st) && st.GetString() == "ok";
                    long droppedCount = 0;
                    if (body.TryGetProperty("mirror", out var mir) &&
                        mir.TryGetProperty("dropped", out var dr))
                        droppedCount = dr.GetInt64();
                    var bqStatus = !built ? "fail" : bridgeOk ? (droppedCount > 0 ? "warn" : "ok") : "warn";
                    var msg = body.TryGetProperty("message", out var m) ? m.GetString() : null;
                    var detail = !built
                        ? "Run ./scripts/broccoliq-build.sh (broccoliq/dist missing)"
                        : bridgeOk
                            ? droppedCount > 0
                                ? $"Bridge ok; mirror queue has dropped events ({droppedCount}). See docs/broccoliq.md"
                                : msg ?? "joy-bridge healthy"
                            : msg ?? "Start control plane or run joy-bridge manually";
                    Add("broccoliq_bridge", bqStatus, detail);
                }
            }
            else
            {
                Add("broccoliq_bridge", "warn",
                    broccoliq.Message ?? "Could not read /api/broccoliq/health");
            }
        }

        var linkedProfile = Environment.GetEnvironmentVariable("JOYZONING_HERMES_PROFILE") ?? "joyzoning";
        if (health.IsSuccess)
        {
            var cfg = await client.GetConfigAsync();
            if (cfg.IsSuccess && cfg.Body.HasValue && cfg.Body.Value.TryGetProperty("profile", out var profileProp))
            {
                var fromApi = profileProp.GetString();
                if (!string.IsNullOrWhiteSpace(fromApi))
                    linkedProfile = fromApi;
            }
        }

        var modelDiag = HermesProfileCatalog.DiagnoseLink(linkedProfile);
        Add(
            "hermes_model_profile",
            modelDiag.ModelsMatch ? "ok" : "warn",
            modelDiag.ModelsMatch
                ? $"Profile '{linkedProfile}' model: {modelDiag.LinkedProfile.Model.Describe()}"
                : modelDiag.Recommendation ?? "Model mismatch between JoyZoning profile and default Hermes config.");

        if (!ctx.Quiet)
        {
            var envelope = new { ok, checks };
            var writeCode = CliOutput.WriteEnvelope(ctx, envelope);
            return writeCode != 0 ? writeCode : ok ? 0 : 1;
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
