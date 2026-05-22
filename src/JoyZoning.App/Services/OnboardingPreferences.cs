using System.Text.Json;
using System.Text.Json.Serialization;

namespace JoyZoning.App.Services;

/// <summary>Local UI preferences for onboarding (not stored in control plane DB).</summary>
public sealed class OnboardingPreferences
{
    public const int CurrentSchemaVersion = 3;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JoyZoning",
        "onboarding.json");

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool WizardCompleted { get; set; }
    public bool WizardDismissed { get; set; }
    public bool AutoConnectHermesOnStartup { get; set; } = true;
    /// <summary>Run zero-config setup on launch (paths, gateway, dashboard, workspace).</summary>
    public bool AutoSetupOnLaunch { get; set; } = true;
    /// <summary>Run diet-hermes setup when the canonical checkout is missing or incomplete.</summary>
    public bool AutoInstallHermesStack { get; set; } = true;
    public bool UseSampleWorkspaceWhenNeeded { get; set; } = true;
    /// <summary>Override install path (default: ~/Downloads/diet-hermes-main-master).</summary>
    public string? DietHermesInstallPath { get; set; }
    /// <summary>Git remote when the canonical folder must be cloned (empty = NousResearch/hermes-agent).</summary>
    public string? DietHermesGitUrl { get; set; }
    public DateTimeOffset? AutoSetupCompletedAt { get; set; }
    public bool SetupBannerDismissed { get; set; }
    public string? LastWorkspacePath { get; set; }
    public bool OpenTuiAfterWizard { get; set; }
    public DateTimeOffset? FirstLaunchAt { get; set; }
    public DateTimeOffset? CelebrationShownAt { get; set; }
    public bool PreferGettingStartedOnLaunch { get; set; } = true;
    public List<string> DismissedSurfaceTips { get; set; } = new();
    public List<string> CompletedOptionalSteps { get; set; } = new();
    public Dictionary<string, DateTimeOffset> StepCompletedAt { get; set; } = new();
    public DateTimeOffset? CoreSetupCompletedAt { get; set; }
    public DateTimeOffset? LastHubVisitAt { get; set; }

    public DateTimeOffset? GetStepCompletedAt(string stepId) =>
        StepCompletedAt.TryGetValue(stepId, out var at) ? at : null;

    public void RecordStepCompleted(string stepId)
    {
        StepCompletedAt[stepId] = DateTimeOffset.UtcNow;
    }

    public static string FormatCompletedAgo(DateTimeOffset completedAt)
    {
        var span = DateTimeOffset.UtcNow - completedAt;
        if (span.TotalMinutes < 2) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    public static void ResetAll()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    public static OnboardingPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new OnboardingPreferences();

            var json = File.ReadAllText(FilePath);
            var prefs = JsonSerializer.Deserialize<OnboardingPreferences>(json) ?? new OnboardingPreferences();
            prefs.DismissedSurfaceTips ??= new List<string>();
            prefs.CompletedOptionalSteps ??= new List<string>();
            prefs.StepCompletedAt ??= new Dictionary<string, DateTimeOffset>();
            return prefs;
        }
        catch
        {
            return new OnboardingPreferences();
        }
    }

    public void Save()
    {
        SchemaVersion = CurrentSchemaVersion;
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        }));
    }

    public void MarkOptionalStep(string stepId)
    {
        if (!CompletedOptionalSteps.Contains(stepId, StringComparer.OrdinalIgnoreCase))
        {
            CompletedOptionalSteps.Add(stepId);
            Save();
        }
    }
}
