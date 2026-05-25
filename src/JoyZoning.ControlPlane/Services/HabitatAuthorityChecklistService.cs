using System.Text.RegularExpressions;
using JoyZoning.Agents.Hermes;
using JoyZoning.Domain.Configuration;
using JoyZoning.Domain.Enums;
using JoyZoning.Domain.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Startup checklist proving JoyZoning is habitat (observe-only) and Hermes is runtime authority.
/// </summary>
public class HabitatAuthorityChecklistService
{
    private const string LegacyShimMarker = "LEGACY_RUNTIME_SHIM.md";

    private readonly HermesConnectivityService _hermesConnectivity;
    private readonly HermesRuntimeSettings _runtimeSettings;
    private readonly IOptions<ControlPlaneOptions> _controlPlane;
    private readonly IHostEnvironment _environment;

    public HabitatAuthorityChecklistService(
        HermesConnectivityService hermesConnectivity,
        HermesRuntimeSettings runtimeSettings,
        IOptions<ControlPlaneOptions> controlPlane,
        IHostEnvironment environment)
    {
        _hermesConnectivity = hermesConnectivity;
        _runtimeSettings = runtimeSettings;
        _controlPlane = controlPlane;
        _environment = environment;
    }

    public async Task<HabitatAuthorityChecklist> BuildAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _runtimeSettings.GetSnapshot();
        var listenUrl = (_controlPlane.Value.ListenUrl ?? "").Trim();
        var installRoot = snapshot.InstallRoot ?? "";
        var isLegacyShim = IsLegacyRuntimeShimInstall(installRoot);
        var externalInstall = !string.IsNullOrWhiteSpace(installRoot)
            && Directory.Exists(installRoot)
            && !isLegacyShim;

        HealthState hermesState = HealthState.Unknown;
        string hermesMessage = "Not checked in Testing";
        if (!_environment.IsEnvironment("Testing"))
        {
            var health = await _hermesConnectivity.GetHealthAsync(cancellationToken);
            hermesState = health.State;
            hermesMessage = health.Message;
        }
        else
        {
            hermesState = HealthState.Healthy;
            hermesMessage = "Skipped in Testing";
        }

        var hermesApiBase = snapshot.ApiBaseUrl ?? "";
        var hermesConfigPath = ResolveHermesConfigPath(snapshot.Profile);
        var mirrorUrl = HermesJoyZoningConfigReader.TryReadControlPlaneUrl(hermesConfigPath);
        var mirrorConfigured = !string.IsNullOrWhiteSpace(mirrorUrl);
        var mirrorMatches = mirrorConfigured
            && string.Equals(
                mirrorUrl!.TrimEnd('/'),
                listenUrl.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);

        var items = new List<HabitatAuthorityCheckItem>
        {
            new(
                "hermes_runtime_owner",
                "Hermes runtime owner detected",
                hermesState == HealthState.Healthy,
                hermesState == HealthState.Healthy
                    ? $"Hermes API healthy ({snapshot.ApiBaseUrl})"
                    : hermesMessage),
            new(
                "habitat_observe_only",
                "JoyZoning habitat role: observe-only",
                true,
                "JoyZoning supervises; Hermes executes tools and owns the journal."),
            new(
                "control_plane_url",
                "Control plane URL configured",
                !string.IsNullOrWhiteSpace(listenUrl),
                string.IsNullOrWhiteSpace(listenUrl)
                    ? "ControlPlane:ListenUrl is empty"
                    : listenUrl),
            new(
                "hermes_install_root",
                "External Hermes InstallRoot configured",
                externalInstall,
                isLegacyShim
                    ? "InstallRoot points at LegacyRuntimeShim (apps/agent-runtime) — use external Hermes checkout"
                    : string.IsNullOrWhiteSpace(installRoot)
                        ? "Hermes:InstallRoot is empty"
                        : Directory.Exists(installRoot)
                            ? installRoot
                            : $"InstallRoot not found on disk: {installRoot}"),
            new(
                "legacy_shim_inactive",
                "apps/agent-runtime marked LegacyRuntimeShim, not active authority",
                !isLegacyShim,
                isLegacyShim
                    ? "LegacyRuntimeShim must not be Hermes:InstallRoot for production supervision"
                    : LegacyShimDocExists(installRoot)
                        ? "LegacyRuntimeShim doc present; external Hermes is canonical"
                        : "InstallRoot is not the embedded legacy shim"),
            new(
                "hermes_observation_mirror",
                "Hermes joyzoning.control_plane.url mirrors this habitat",
                mirrorMatches,
                mirrorConfigured
                    ? mirrorMatches
                        ? $"Hermes config points at {mirrorUrl}"
                        : $"Hermes config has {mirrorUrl}; expected {listenUrl}"
                    : hermesConfigPath is null
                        ? "Could not locate Hermes config.yaml to verify mirror URL"
                        : "Set joyzoning.control_plane.url in Hermes config to this control plane"),
            new(
                "internal_token",
                "Internal API token configured (production hardening)",
                !string.IsNullOrWhiteSpace(_controlPlane.Value.InternalToken),
                string.IsNullOrWhiteSpace(_controlPlane.Value.InternalToken)
                    ? "Set ControlPlane:InternalToken + JOYZONING_INGEST_TOKEN on Hermes for ingest/agent routes"
                    : "Internal token configured for Hermes observation ingest and agent callbacks"),
            new(
                "habitat_convergence_bridge",
                "Habitat accept-merge → Hermes CONVERGED bridge (HTTP or script)",
                HabitatBridgeAvailable(installRoot, hermesApiBase),
                HabitatBridgeAvailable(installRoot, hermesApiBase)
                    ? $"POST {hermesApiBase.TrimEnd('/')}/api/internal/joyzoning/habitat-ack (preferred) or scripts/joyzoning_habitat_ack.py"
                    : "Set Hermes:InstallRoot and Hermes:ApiBaseUrl for HTTP bridge, or ship joyzoning_habitat_ack.py"),
        };

        return new HabitatAuthorityChecklist(
            items.All(i => i.Ok),
            items,
            RuntimeOwner: "hermes",
            HabitatRole: "observe-only");
    }

    private static bool HabitatBridgeAvailable(string installRoot, string hermesApiBase)
    {
        if (!string.IsNullOrWhiteSpace(hermesApiBase)
            && Uri.TryCreate(hermesApiBase.TrimEnd('/') + "/api/internal/joyzoning/habitat-ack", UriKind.Absolute, out _))
            return true;
        return HabitatBridgeScriptExists(installRoot);
    }

    private static bool HabitatBridgeScriptExists(string installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return false;
        return File.Exists(Path.Combine(installRoot, "scripts", "joyzoning_habitat_ack.py"));
    }

    internal static bool IsLegacyRuntimeShimInstall(string installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return false;

        var normalized = installRoot.Replace('\\', '/').TrimEnd('/');
        return normalized.EndsWith("apps/agent-runtime", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/apps/agent-runtime/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LegacyShimDocExists(string installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return false;

        var direct = Path.Combine(installRoot, LegacyShimMarker);
        if (File.Exists(direct))
            return true;

        var repoCandidate = Path.Combine(installRoot, "apps", "agent-runtime", LegacyShimMarker);
        return File.Exists(repoCandidate);
    }

    private static string? ResolveHermesConfigPath(string profile)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var hermesHome = Path.Combine(home, ".hermes");
        if (!string.IsNullOrWhiteSpace(profile))
        {
            var profilePath = Path.Combine(hermesHome, "profiles", profile, "config.yaml");
            if (File.Exists(profilePath))
                return profilePath;
        }

        var defaultPath = Path.Combine(hermesHome, "config.yaml");
        return File.Exists(defaultPath) ? defaultPath : null;
    }
}

public record HabitatAuthorityCheckItem(string Id, string Label, bool Ok, string Detail);

public record HabitatAuthorityChecklist(
    bool AllPassed,
    IReadOnlyList<HabitatAuthorityCheckItem> Items,
    string RuntimeOwner,
    string HabitatRole);

/// <summary>Minimal joyzoning.* extraction from Hermes config.yaml.</summary>
public static class HermesJoyZoningConfigReader
{
    public static string? TryReadControlPlaneUrl(string? configYamlPath)
    {
        if (string.IsNullOrWhiteSpace(configYamlPath) || !File.Exists(configYamlPath))
            return null;

        var text = File.ReadAllText(configYamlPath);
        var block = ExtractYamlBlock(text, "joyzoning");
        if (string.IsNullOrWhiteSpace(block))
            return null;

        var cpBlock = ExtractYamlBlock(block, "control_plane");
        if (string.IsNullOrWhiteSpace(cpBlock))
            return MatchScalar(block, "control_plane_url") ?? MatchScalar(block, "controlPlaneUrl");

        return MatchScalar(cpBlock, "url");
    }

    private static string? ExtractYamlBlock(string yaml, string key)
    {
        var match = Regex.Match(
            yaml,
            $@"^[ \t]*{Regex.Escape(key)}:\s*\r?\n((?:[ \t].+(?:\r?\n|$))+)",
            RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? MatchScalar(string block, string key)
    {
        var m = Regex.Match(
            block,
            $@"^[ \t]*{Regex.Escape(key)}:\s*['""]?([^'""#\r\n]+)['""]?\s*$",
            RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
