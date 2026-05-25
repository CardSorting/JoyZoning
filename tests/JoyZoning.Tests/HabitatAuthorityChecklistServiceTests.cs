using JoyZoning.ControlPlane.Services;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class HabitatAuthorityChecklistServiceTests
{
    [Theory]
    [InlineData("/Users/dev/JoyZoning/apps/agent-runtime")]
    [InlineData("C:\\JoyZoning\\apps\\agent-runtime")]
    [InlineData("/repo/apps/agent-runtime/")]
    public void Legacy_runtime_shim_install_root_is_detected(string installRoot)
    {
        Assert.True(HabitatAuthorityChecklistService.IsLegacyRuntimeShimInstall(installRoot));
    }

    [Theory]
    [InlineData("/Users/dev/diet-hermes-main-master")]
    [InlineData("")]
    public void External_hermes_install_root_is_not_legacy_shim(string installRoot)
    {
        Assert.False(HabitatAuthorityChecklistService.IsLegacyRuntimeShimInstall(installRoot));
    }

    [Fact]
    public void Hermes_config_reader_extracts_control_plane_url()
    {
        var yaml = """
            joyzoning:
              control_plane:
                url: http://127.0.0.1:9470
              enabled: true
            """;
        var path = Path.Combine(Path.GetTempPath(), "jz-hermes-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, yaml);
        try
        {
            var url = HermesJoyZoningConfigReader.TryReadControlPlaneUrl(path);
            Assert.Equal("http://127.0.0.1:9470", url);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
