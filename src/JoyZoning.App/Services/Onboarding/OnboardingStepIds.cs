namespace JoyZoning.App.Services.Onboarding;

/// <summary>Stable identifiers for onboarding steps (persisted in preferences).</summary>
public static class OnboardingStepIds
{
    public const string ControlPlane = "control_plane";
    public const string CliPaths = "cli";
    public const string Gateway = "gateway";
    public const string Dashboard = "dashboard";
    public const string Workspace = "workspace";
    public const string FirstChat = "first_chat";
    public const string FirstDispatch = "first_dispatch";
    public const string TuiConnected = "tui_connected";
}

public static class OnboardingSurfaceIds
{
    public const string GettingStarted = "getting_started";
    public const string Manager = "manager";
    public const string Kanban = "kanban";
    public const string Execution = "execution";
    public const string Workspace = "workspace";
    public const string Timeline = "timeline";
    public const string Approvals = "approvals";
}
