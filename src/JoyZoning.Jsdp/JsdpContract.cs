namespace JoyZoning.Jsdp;

/// <summary>Versioned contracts for on-disk JSDP artifacts (external planning adapter).</summary>
public static class JsdpContract
{
    public const string PlanningContextVersion = "1";
    public const string ExternalPlanVersion = "1";
    public const int MaxPlanNodes = 64;
    public const int MaxPlanFileBytes = 2 * 1024 * 1024;

    public const string HorizonContextVersion = "1";
    public const string HorizonProposalVersion = "1";
    public const int MinHorizonNodes = 3;
    public const int MaxHorizonNodes = 5;
    public const int DefaultHorizonNodes = 5;
    public const int MaxHorizonLedgerSummaries = 5;
    public const int MaxHorizonContextBytes = 32 * 1024;
}
