using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JSDPRepairNodeFactory
{
    public JsdpNode CreateRepairNode(JsdpNode failed, JsdpRun run)
    {
        var repairId = AllocateRepairId(failed.Id, run);
        return new JsdpNode
        {
            Id = repairId,
            Title = $"Repair {failed.Title}",
            Intent = $"Repair failures from node {failed.Id}: isolate fix for {failed.Intent} without unrelated mutation.",
            Prompt = $"Repair node for {failed.Id}. Address verification failures only within allowed surfaces.",
            Dependencies = failed.Dependencies.ToList(),
            AcceptanceCriteria =
            [
                $"Root cause from node {failed.Id} is fixed.",
                "All verification commands pass.",
                "No changes outside allowed mutation surface.",
            ],
            VerificationCommands = failed.VerificationCommands.ToList(),
            AllowedMutationSurface = failed.AllowedMutationSurface.ToList(),
            Status = JsdpNodeStatus.Pending,
            Outputs = failed.Outputs.ToList(),
            RepairOf = failed.Id,
        };
    }

    private static string AllocateRepairId(string originalId, JsdpRun run)
    {
        var baseId = originalId.TrimEnd('R', 'r');
        var attempt = 1;
        while (true)
        {
            var candidate = $"{baseId}R{attempt}";
            if (!run.Nodes.ContainsKey(candidate))
                return candidate;
            attempt++;
        }
    }
}
