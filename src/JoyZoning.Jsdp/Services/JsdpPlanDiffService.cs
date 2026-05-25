using JoyZoning.Jsdp.Models;

namespace JoyZoning.Jsdp.Services;

public sealed class JsdpPlanDiffService
{
    private readonly JsdpExternalPlanValidator _validator = new();

    public JsdpPlanDiffResult Diff(string planPath, JSDPStateStore store)
    {
        var fullPath = Path.GetFullPath(planPath);
        var plan = _validator.LoadPlanFile(fullPath);
        ProjectSpecAnalysis? analysis = null;
        if (store.IsInitialized)
        {
            try
            {
                analysis = store.LoadProjectSpec().Analysis;
            }
            catch
            {
                // optional
            }
        }

        var validation = _validator.Validate(plan, analysis);
        var proposedIds = validation.NormalizedNodes?.Keys.ToHashSet(StringComparer.Ordinal)
            ?? plan.Nodes
                .Select((n, i) => string.IsNullOrWhiteSpace(n.Id) ? (i + 1).ToString("D3") : n.Id.Trim())
                .ToHashSet(StringComparer.Ordinal);

        var currentIds = store.IsInitialized
            ? store.LoadRun().Nodes.Keys.ToHashSet(StringComparer.Ordinal)
            : [];

        return new JsdpPlanDiffResult
        {
            PlanPath = fullPath,
            PlanValid = validation.Valid,
            CurrentNodeCount = currentIds.Count,
            ProposedNodeCount = proposedIds.Count,
            OnlyInPlan = proposedIds.Except(currentIds).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            OnlyInRun = currentIds.Except(proposedIds).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            InBoth = proposedIds.Intersect(currentIds).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            Validation = validation,
        };
    }
}
