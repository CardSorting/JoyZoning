namespace JoyZoning.Tests;

/// <summary>xUnit traits for tiered test runs (see scripts/run-tests.sh).</summary>
public static class TestCategories
{
    public const string Key = "Category";
    public const string Unit = "Unit";
    public const string Integration = "Integration";
    public const string Dogfood = "Dogfood";
}
