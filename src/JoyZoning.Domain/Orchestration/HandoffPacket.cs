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
}
