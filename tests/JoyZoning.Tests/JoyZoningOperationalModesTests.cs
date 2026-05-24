using JoyZoning.Domain.Orchestration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class JoyZoningOperationalModesTests
{
    [Fact]
    public void Defines_four_distinct_modes()
    {
        Assert.Equal(4, JoyZoningOperationalModes.All.Count);
        Assert.Contains(JoyZoningOperationalModes.All, m => m.Mode == JoyZoningOperationalMode.Planning);
        Assert.Contains(JoyZoningOperationalModes.All, m => m.Mode == JoyZoningOperationalMode.Execution);
        Assert.Contains(JoyZoningOperationalModes.All, m => m.Mode == JoyZoningOperationalMode.Review);
        Assert.Contains(JoyZoningOperationalModes.All, m => m.Mode == JoyZoningOperationalMode.HabitatAmbient);
    }

    [Fact]
    public void Planning_mode_owns_kanban_not_merge_queue()
    {
        var planning = JoyZoningOperationalModes.Get(JoyZoningOperationalMode.Planning);
        Assert.Contains("WorkTask", planning.PrimaryEntities);
        Assert.DoesNotContain(planning.ApiRouteHints, h => h.Contains("merge-queue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Review_mode_owns_merge_and_preflight()
    {
        var review = JoyZoningOperationalModes.Get(JoyZoningOperationalMode.Review);
        Assert.Contains(review.ApiRouteHints, h => h.Contains("merge-queue", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(review.PrimaryEntities, e => e.Contains("OperatorDecisionSummary", StringComparison.Ordinal));
    }

    [Fact]
    public void Habitat_is_not_canonical_operational_surface()
    {
        var habitat = JoyZoningOperationalModes.Get(JoyZoningOperationalMode.HabitatAmbient);
        Assert.False(habitat.IsCanonicalOperationalSurface);
    }

    [Theory]
    [InlineData("/api/sessions/x/merge-queue", JoyZoningOperationalMode.Review)]
    [InlineData("/api/sessions/x/parallel-workers", JoyZoningOperationalMode.Execution)]
    [InlineData("/api/tasks/x/workspace/changed", JoyZoningOperationalMode.Execution)]
    [InlineData("/api/tasks", JoyZoningOperationalMode.Planning)]
    public void Infers_mode_from_api_path(string path, JoyZoningOperationalMode expected)
    {
        Assert.Equal(expected, JoyZoningOperationalModes.InferFromApiPath(path));
    }
}
