namespace JoyZoning.Domain.Configuration;

/// <summary>Bounded YOLO / autopilot accept policy for ReadyForReview workers.</summary>
public class AuthorityOptions
{
    public const string SectionName = "Authority";

    /// <summary>When false, no automatic accept after verification (Conservative-like).</summary>
    public bool AutopilotEnabled { get; set; } = true;

    /// <summary>Re-evaluate ReadyForReview leases during lease reconciliation ticks.</summary>
    public bool ReconcileReadyForReview { get; set; } = true;

    public AuthorityProfileKind DefaultProfile { get; set; } = AuthorityProfileKind.BalancedAuto;

    /// <summary>Per-session overrides keyed by session id (guid string) or session name.</summary>
    public Dictionary<string, AuthorityProfileKind> SessionProfileOverrides { get; set; } = new();

    public CustomAuthorityRules Custom { get; set; } = new();
}

public enum AuthorityProfileKind
{
    Conservative,
    BalancedAuto,
    Yolo,
    Custom,
}

public sealed class CustomAuthorityRules
{
    public int MaxChangedFiles { get; set; } = 25;
    public List<string> DenyPathGlobs { get; set; } = [];
    public List<string> AllowPathGlobs { get; set; } = [];
    public List<string> AllowedRiskLevels { get; set; } = ["Low", "Medium"];
}
