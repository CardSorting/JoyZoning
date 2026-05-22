namespace JoyZoning.App.Services;

/// <summary>Shared app-level services for view models.</summary>
public static class AppServices
{
    public static ControlPlaneClient ControlPlane { get; } = new();
    public static OperatorHubClient Hub { get; } = new();
}
