using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JsdpHorizonDiffService
{
    public JsdpHorizonDiffResult Diff(
        JsdpHorizonProposalDocument proposal,
        JsdpHorizonValidationResult validation,
        JsdpRun run,
        IReadOnlyDictionary<string, string> projectedIdMap)
    {
        var result = new JsdpHorizonDiffResult
        {
            PlanValid = validation.Valid,
            CurrentDagSize = run.Nodes.Count,
            Validation = validation,
        };

        if (!validation.Valid || validation.NormalizedNodes is null)
            return result;

        foreach (var (tempId, node) in validation.NormalizedNodes)
        {
            var finalId = projectedIdMap.TryGetValue(tempId, out var mapped) ? mapped : tempId;
            result.ProjectedAppends.Add(new JsdpHorizonDiffNode
            {
                ProjectedId = finalId,
                Title = node.Title,
                Dependencies = node.Dependencies.ToList(),
            });
        }

        return result;
    }
}
