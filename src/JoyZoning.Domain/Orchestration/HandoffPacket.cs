using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

/// <summary>DietCode handoff payload generated from a kanban card.</summary>
public sealed class HandoffPacket
{
    public Guid CardId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ForbiddenPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> VerificationCommands { get; init; } = Array.Empty<string>();
    public string RollbackInstructions { get; init; } = string.Empty;
    public LeaseRiskLevel RiskLevel { get; init; }
    public string WorktreePath { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string SessionWorkspaceRoot { get; init; } = string.Empty;
    public DeliveryRoleKind DeliveryRole { get; init; } = DeliveryRoleKind.Unknown;
    public int DeliveryWave { get; init; }
    public int FoundationFilesSeeded { get; init; }
    public string? Protocol { get; init; }
    public int? JsdpSequence { get; init; }
    public Guid? DeliveryChainId { get; init; }
    public bool MergeGateRequired { get; init; }
    public IReadOnlyList<string> RequiredOutputSections { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ComplianceWarnings { get; init; } = Array.Empty<string>();
}
