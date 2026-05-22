using System.Text.Json;
using JoyZoning.Domain.Entities;
using JoyZoning.Domain.Enums;

namespace JoyZoning.Domain.Orchestration;

public static class HandoffPacketBuilder
{
    public static HandoffPacket Build(WorkTask task, string worktreePath, string branchName)
    {
        var risk = LeaseRiskMapper.FromTaskRisk(task.Risk);
        var (allowed, forbidden) = DefaultPathRules(task, risk);

        return new HandoffPacket
        {
            CardId = task.Id,
            Title = task.Title,
            Objective = task.Description,
            AcceptanceCriteria = ParseLines(task.Description),
            Constraints =
            [
                "Do not mark the kanban card done.",
                "Use only the allowed paths for file changes.",
                "Stop and set status to blocked if requirements are unclear.",
            ],
            AllowedPaths = allowed,
            ForbiddenPaths = forbidden,
            VerificationCommands = DefaultVerificationCommands(risk),
            RollbackInstructions =
                $"Discard branch {branchName} and remove worktree at {worktreePath} if execution is revoked.",
            RiskLevel = risk,
            WorktreePath = worktreePath,
            BranchName = branchName,
        };
    }

    public static string Serialize(HandoffPacket packet) =>
        JsonSerializer.Serialize(packet);

    public static HandoffPacket Deserialize(string json) =>
        JsonSerializer.Deserialize<HandoffPacket>(json)
        ?? throw new InvalidOperationException("Invalid handoff packet JSON.");

    public static string ToExecutorPrompt(HandoffPacket packet)
    {
        var criteria = packet.AcceptanceCriteria.Count > 0
            ? string.Join("\n", packet.AcceptanceCriteria.Select(c => $"- {c}"))
            : "- (derive from objective)";

        return $"""
            ## JoyZoning execution handoff
            Card: {packet.CardId}
            Title: {packet.Title}
            Risk: {packet.RiskLevel}
            Worktree: {packet.WorktreePath}
            Branch: {packet.BranchName}

            ### Objective
            {packet.Objective}

            ### Acceptance criteria
            {criteria}

            ### Constraints
            {string.Join("\n", packet.Constraints.Select(c => $"- {c}"))}

            ### Allowed paths
            {string.Join(", ", packet.AllowedPaths)}

            ### Forbidden paths
            {string.Join(", ", packet.ForbiddenPaths)}

            ### Verification commands
            {string.Join("\n", packet.VerificationCommands.Select(c => $"- `{c}`"))}

            ### Rollback
            {packet.RollbackInstructions}

            You must not mark this kanban card done. When finished, move the lease to verifying, then ready_for_review after checks pass.
            """;
    }

    private static (IReadOnlyList<string> Allowed, IReadOnlyList<string> Forbidden) DefaultPathRules(
        WorkTask task,
        LeaseRiskLevel risk)
    {
        var forbidden = new List<string> { ".git/", "node_modules/", ".env", "secrets/" };
        var allowed = new List<string> { "src/", "tests/", "docs/", "scripts/" };

        if (risk == LeaseRiskLevel.Critical)
            forbidden.AddRange(["infra/", "deploy/", "migrations/"]);

        return (allowed, forbidden);
    }

    private static IReadOnlyList<string> DefaultVerificationCommands(LeaseRiskLevel risk) =>
        risk == LeaseRiskLevel.Critical
            ? ["dotnet build", "dotnet test --no-build"]
            : ["dotnet build"];

    private static IReadOnlyList<string> ParseLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0)
            .Take(12)
            .ToList();
}
