using JoyZoning.Agents.Hermes;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class HermesProfileEnvTests
{
    [Fact]
    public void ResolveEnvFileCandidates_IncludesProfilePath()
    {
        var home = Path.Combine(Path.GetTempPath(), "jz-test-" + Guid.NewGuid().ToString("N"));
        var profileDir = Path.Combine(home, "profiles", "joyzoning");
        Directory.CreateDirectory(profileDir);
        var envFile = Path.Combine(profileDir, ".env");
        File.WriteAllText(envFile, "API_SERVER_KEY=test-key-123\n");

        try
        {
            Environment.SetEnvironmentVariable("HERMES_HOME", home);
            var key = HermesProfileEnv.TryReadApiServerKey("joyzoning");
            Assert.Equal("test-key-123", key);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HERMES_HOME", null);
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public void ResolveEnvFileCandidates_WhenHermesHomeIsProfileDir_ReadsDirectEnv()
    {
        var profileHome = Path.Combine(Path.GetTempPath(), "jz-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profileHome);
        File.WriteAllText(Path.Combine(profileHome, ".env"), "API_SERVER_KEY=direct-profile-key\n");

        try
        {
            Environment.SetEnvironmentVariable("HERMES_HOME", profileHome);
            var key = HermesProfileEnv.TryReadApiServerKey("joyzoning");
            Assert.Equal("direct-profile-key", key);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HERMES_HOME", null);
            Directory.Delete(profileHome, recursive: true);
        }
    }
}
