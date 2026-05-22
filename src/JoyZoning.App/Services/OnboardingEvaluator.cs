using JoyZoning.App.Services.Onboarding;

namespace JoyZoning.App.Services;

public static class OnboardingEvaluator
{
    public static PathValidationResult ValidateInstallRoot(string? installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return new PathValidationResult(false, "Install root is required.");

        var path = installRoot.Trim();
        if (!Directory.Exists(path))
            return new PathValidationResult(false, "Folder does not exist.");

        var localCli = HermesInstallPaths.FindHermesCliInCheckout(path);
        if (localCli is not null)
            return new PathValidationResult(true, $"Found hermes at {localCli}");

        foreach (var candidate in new[] { "hermes", "/usr/local/bin/hermes" })
        {
            try
            {
                if (candidate == "hermes" || File.Exists(candidate))
                    return new PathValidationResult(true, "hermes found on PATH (install root used as working directory).");
            }
            catch
            {
                // ignore
            }
        }

        return new PathValidationResult(
            false,
            "No hermes CLI in this folder. JoyZoning can install Hermes automatically on the next setup run.");
    }

    public static async Task<OnboardingSnapshot> AssessAsync(
        bool hasActiveSession,
        CancellationToken cancellationToken = default)
    {
        var cpOk = await AppServices.ControlPlane.IsHealthyAsync();
        if (!cpOk)
        {
            return new OnboardingSnapshot(
                false, false, false, false, false, false,
                0, "JoyZoning services did not start — quit and reopen the app.");
        }

        var settings = await AppServices.ControlPlane.GetSettingsAsync();
        var pathCheck = ValidateInstallRoot(settings?.InstallRoot);
        var gw = await AppServices.ControlPlane.GetHermesHealthAsync();
        var dash = await AppServices.ControlPlane.GetDashboardStatusAsync();

        var cliOk = dash?.HermesCliFound == true && pathCheck.IsValid;
        var gwOk = gw?.State == "Healthy";
        var dashOk = dash?.Ready == true;

        var done = 0;
        if (cpOk) done++;
        if (cliOk) done++;
        if (gwOk) done++;
        if (dashOk) done++;
        if (hasActiveSession) done++;

        var percent = (int)Math.Round(done / 5.0 * 100);
        var message = dashOk && gwOk && hasActiveSession
            ? "You're ready — try Manager Chat or explore optional milestones below."
            : !pathCheck.IsValid
                ? pathCheck.Message
                : !gwOk
                    ? "Hermes is still starting — tap Smart setup or wait a moment and refresh."
                    : !dashOk
                        ? "Almost there — connecting the Hermes dashboard for chat and kanban."
                        : "Pick a project folder, or use the sample workspace to try JoyZoning right away.";

        return new OnboardingSnapshot(cpOk, cliOk, gwOk, dashOk, hasActiveSession, pathCheck.IsValid, percent, message);
    }

    public static IReadOnlyList<OnboardingChecklistItem> ToChecklist(OnboardingSnapshot s)
    {
        bool Ok(string id) => id switch
        {
            OnboardingStepIds.ControlPlane => s.ControlPlaneOk,
            OnboardingStepIds.CliPaths => s.CliOk,
            OnboardingStepIds.Gateway => s.GatewayOk,
            OnboardingStepIds.Dashboard => s.DashboardOk,
            OnboardingStepIds.Workspace => s.WorkspaceOk,
            _ => false,
        };

        return OnboardingCatalog.CoreSteps
            .Select(def => new OnboardingChecklistItem(
                def.Id,
                def.Title,
                Ok(def.Id),
                def.FixLabel))
            .ToList();
    }

    public static IReadOnlyList<OnboardingStepState> BuildStepStates(OnboardingSnapshot s, bool hasSession)
    {
        bool Complete(string id) => id switch
        {
            OnboardingStepIds.ControlPlane => s.ControlPlaneOk,
            OnboardingStepIds.CliPaths => s.CliOk && s.PathsValid,
            OnboardingStepIds.Gateway => s.GatewayOk,
            OnboardingStepIds.Dashboard => s.DashboardOk,
            OnboardingStepIds.Workspace => hasSession,
            _ => false,
        };

        bool Blocked(string id) => id switch
        {
            OnboardingStepIds.Gateway => !s.ControlPlaneOk || !s.PathsValid,
            OnboardingStepIds.Dashboard => !s.GatewayOk,
            OnboardingStepIds.Workspace => !s.DashboardOk && !s.GatewayOk,
            _ => !s.ControlPlaneOk,
        };

        string? BlockReason(string id) => id switch
        {
            OnboardingStepIds.Gateway when !s.PathsValid => "Set a valid install path first.",
            OnboardingStepIds.Dashboard when !s.GatewayOk => "Start the API gateway first.",
            OnboardingStepIds.Workspace when !s.GatewayOk => "Connect Hermes before opening a workspace.",
            _ => null,
        };

        string? RequiresLabel(string id) => id switch
        {
            OnboardingStepIds.Gateway when Blocked(id) => "Requires: Hermes install path",
            OnboardingStepIds.Dashboard when Blocked(id) => "Requires: API gateway",
            OnboardingStepIds.Workspace when Blocked(id) => "Requires: Hermes connection",
            _ => null,
        };

        return OnboardingCatalog.CoreSteps
            .Select(def => new OnboardingStepState(
                def,
                Complete(def.Id),
                Blocked(def.Id) && !Complete(def.Id),
                BlockReason(def.Id),
                RequiresLabel(def.Id)))
            .ToList();
    }

    public static IReadOnlyList<OnboardingDiagnostic> BuildDiagnostics(OnboardingSnapshot s, bool hermesFullyReady)
    {
        var list = new List<OnboardingDiagnostic>();

        if (!s.ControlPlaneOk)
        {
            list.Add(new(
                "cp_offline",
                "Control plane unreachable",
                "JoyZoning could not reach http://127.0.0.1:9470. Restart the app or run JoyZoning.ControlPlane manually.",
                OnboardingDiagnosticSeverity.Error,
                OnboardingStepIds.ControlPlane));
        }

        if (!s.PathsValid)
        {
            list.Add(new(
                "paths_invalid",
                "Install path not configured",
                "Select your diet-hermes checkout (Auto-detect scans Downloads and env vars).",
                OnboardingDiagnosticSeverity.Error,
                OnboardingStepIds.CliPaths));
        }
        else if (!s.CliOk)
        {
            list.Add(new(
                "cli_missing",
                "hermes CLI not found",
                "Expected .venv/bin/hermes under the install root or hermes on PATH.",
                OnboardingDiagnosticSeverity.Error,
                OnboardingStepIds.CliPaths));
        }

        if (s.ControlPlaneOk && s.PathsValid && !s.GatewayOk)
        {
            list.Add(new(
                "gateway_down",
                "API gateway not healthy",
                "Use Start gateway or Connect Hermes from Getting Started.",
                OnboardingDiagnosticSeverity.Warning,
                OnboardingStepIds.Gateway));
        }

        if (s.GatewayOk && !s.DashboardOk)
        {
            list.Add(new(
                "dashboard_token",
                "Dashboard needs connection",
                "JoyZoning will start `hermes dashboard` and scrape the session token automatically.",
                OnboardingDiagnosticSeverity.Warning,
                OnboardingStepIds.Dashboard));
        }

        if (hermesFullyReady && !s.WorkspaceOk)
        {
            list.Add(new(
                "no_workspace",
                "No workspace open",
                "Open a project folder to enable Manager Chat, Kanban, and file review.",
                OnboardingDiagnosticSeverity.Info,
                OnboardingStepIds.Workspace));
        }

        if (list.Count == 0 && s.ProgressPercent >= 100)
        {
            list.Add(new(
                "ready",
                "All core checks passed",
                "Optional milestones below help you explore Manager Chat, dispatch, and TUI.",
                OnboardingDiagnosticSeverity.Info));
        }

        return list;
    }
}

public record PathValidationResult(bool IsValid, string Message);

public record OnboardingChecklistItem(string Id, string Label, bool IsComplete, string? FixHint = null);
