using JoyZoning.App.Services.Onboarding;
using JoyZoning.App.ViewModels;

namespace JoyZoning.App.Services;

/// <summary>
/// Zero-config first-run pipeline — user does not need to know paths, gateway, or tokens.
/// </summary>
public static class OnboardingAutoSetup
{
    public const string SampleWorkspaceFolderName = "getting-started";

    public record AutoSetupResult(
        bool CoreComplete,
        bool WorkspaceReady,
        bool NeedsWorkspacePicker,
        string? WorkspacePath,
        string Message);

    public static string SampleWorkspacePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JoyZoning",
        "workspaces",
        SampleWorkspaceFolderName);

    public static async Task<AutoSetupResult> RunAsync(
        HermesConnectionViewModel connection,
        Action<string> reportStatus,
        Action<int> reportProgress,
        CancellationToken cancellationToken = default)
    {
        reportProgress(0);
        reportStatus("Starting JoyZoning services…");

        await ControlPlaneHost.EnsureRunningAsync();
        reportProgress(10);

        if (!await AppServices.ControlPlane.IsHealthyAsync())
        {
            return new AutoSetupResult(false, false, false, null,
                "Control plane did not start. Restart the app or run JoyZoning.ControlPlane.");
        }

        reportStatus("Checking Hermes on your Mac…");
        await connection.LoadAsync();

        var settings = await AppServices.ControlPlane.GetSettingsAsync();
        var prefs = OnboardingPreferences.Load();
        var installRoot = OnboardingPathDiscovery.BestGuess(settings?.InstallRoot)
            ?? OnboardingPathDiscovery.BestGuess(connection.InstallRoot);

        if (string.IsNullOrWhiteSpace(installRoot)
            || !OnboardingEvaluator.ValidateInstallRoot(installRoot).IsValid)
        {
            if (prefs.AutoInstallHermesStack)
            {
                reportStatus("Setting up diet-hermes at ~/Downloads/diet-hermes-main-master…");
                var install = await HermesStackInstaller.EnsureInstalledAsync(
                    reportStatus,
                    reportProgress,
                    cancellationToken);

                if (!install.Success || string.IsNullOrWhiteSpace(install.DietHermesRoot))
                {
                    return new AutoSetupResult(false, false, false, null, install.Message);
                }

                installRoot = install.DietHermesRoot;
                reportStatus(install.Message);
            }
            else
            {
                return new AutoSetupResult(false, false, false, null,
                    "Hermes is not installed. Turn on automatic install in Settings, or open Getting Started.");
            }
        }

        connection.InstallRoot = installRoot;
        if (string.IsNullOrWhiteSpace(connection.ApiBaseUrl))
            connection.ApiBaseUrl = settings?.ApiBaseUrl ?? "http://127.0.0.1:8642";
        if (string.IsNullOrWhiteSpace(connection.DashboardBaseUrl))
            connection.DashboardBaseUrl = settings?.DashboardBaseUrl ?? "http://127.0.0.1:9119";

        await connection.SavePathsCommand.ExecuteAsync(null);
        reportProgress(25);

        reportStatus("Starting Hermes API gateway…");
        await connection.EnsureGatewayCommand.ExecuteAsync(null);
        reportProgress(45);

        reportStatus("Connecting Hermes dashboard and session…");
        var dashOk = await connection.EnsureDashboardAsync();
        reportProgress(70);

        if (!dashOk)
        {
            return new AutoSetupResult(false, false, false, null,
                "Hermes dashboard did not connect. Check that diet-hermes is installed and try Getting Started → Smart setup.");
        }

        var workspacePath = ResolveWorkspacePath();
        if (workspacePath is not null)
        {
            reportStatus("Opening your workspace…");
            reportProgress(100);
            return new AutoSetupResult(true, true, false, workspacePath,
                "All set — opening your workspace.");
        }

        reportProgress(100);
        return new AutoSetupResult(true, false, true, null,
            "Hermes is ready — pick a folder or use the sample workspace.");
    }

    public static string? ResolveWorkspacePath()
    {
        var prefs = OnboardingPreferences.Load();

        if (!string.IsNullOrWhiteSpace(prefs.LastWorkspacePath)
            && Directory.Exists(prefs.LastWorkspacePath))
            return prefs.LastWorkspacePath;

        var cwd = Directory.GetCurrentDirectory();
        if (LooksLikeUserProject(cwd))
            return cwd;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var candidate in new[]
                 {
                     Path.Combine(home, "Desktop", "JoyZoning"),
                     Path.Combine(home, "Documents", "JoyZoning"),
                     Path.Combine(home, "Projects"),
                     Path.Combine(home, "dev"),
                     Path.Combine(home, "Developer"),
                 })
        {
            if (Directory.Exists(candidate) && LooksLikeUserProject(candidate))
                return candidate;
        }

        if (prefs.UseSampleWorkspaceWhenNeeded)
            return EnsureSampleWorkspace();

        return null;
    }

    public static string EnsureSampleWorkspace()
    {
        var path = SampleWorkspacePath;
        Directory.CreateDirectory(path);

        var readme = Path.Combine(path, "README.md");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme,
                """
                # JoyZoning getting-started workspace

                JoyZoning created this folder automatically so you can try Manager Chat and Kanban
                without picking a project first.

                Replace it anytime with **Project → Open Workspace** and choose your real repo.
                """);
        }

        return path;
    }

    private static bool LooksLikeUserProject(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
        }
        catch
        {
            return false;
        }

        if (!Directory.Exists(path))
            return false;

        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        if (name is "JoyZoning" or "JoyZoning.App" or "bin" or "obj" or "node_modules")
            return false;

        if (path.Contains("JoyZoning.App", StringComparison.OrdinalIgnoreCase)
            && path.Contains("bin", StringComparison.OrdinalIgnoreCase))
            return false;

        return Directory.Exists(Path.Combine(path, ".git"))
               || File.Exists(Path.Combine(path, "package.json"))
               || File.Exists(Path.Combine(path, "pyproject.toml"))
               || File.Exists(Path.Combine(path, "Cargo.toml"))
               || File.Exists(Path.Combine(path, "go.mod"))
               || File.Exists(Path.Combine(path, "JoyZoning.sln"))
               || File.Exists(Path.Combine(path, "AGENTS.md"))
               || File.Exists(Path.Combine(path, "CLAUDE.md"));
    }
}
