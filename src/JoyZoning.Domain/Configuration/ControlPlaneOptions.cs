namespace JoyZoning.Domain.Configuration;

public class ControlPlaneOptions
{
    public const string SectionName = "ControlPlane";

    public string ListenUrl { get; set; } = "http://127.0.0.1:9470";

    /// <summary>
    /// Shared secret for internal routes (Hermes observation ingest, agent callbacks).
    /// When set, callers must send X-JoyZoning-Internal-Token. Empty = dev-only open (not for production).
    /// </summary>
    public string? InternalToken { get; set; }
}
