using System.Text.Json;
using JoyZoning.Domain.Configuration;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class LeaseRuntimeConfigTests
{
    [Fact]
    public void LeaseRuntimeOptions_default_metadata_only_accept_is_false()
    {
        var options = new LeaseRuntimeOptions();
        Assert.False(options.MetadataOnlyAcceptResult);
    }

    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public void Shipped_appsettings_keep_metadata_only_accept_disabled(string fileName)
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "src", "JoyZoning.ControlPlane", fileName);
        Assert.True(File.Exists(path), $"Expected config at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("LeaseRuntime", out var leaseRuntime),
            $"{fileName} should define LeaseRuntime section");

        if (leaseRuntime.TryGetProperty("MetadataOnlyAcceptResult", out var flag))
            Assert.False(flag.GetBoolean(), $"{fileName} must not enable MetadataOnlyAcceptResult");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "src", "JoyZoning.ControlPlane", "appsettings.json");
            if (File.Exists(candidate))
                return dir;

            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate JoyZoning.ControlPlane/appsettings.json from test output.");
    }
}
