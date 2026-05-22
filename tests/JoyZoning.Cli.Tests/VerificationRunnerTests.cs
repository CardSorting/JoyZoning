using JoyZoning.Cli;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class VerificationRunnerTests
{
    [Fact]
    public void Summarize_includes_exit_code()
    {
        var summary = VerificationRunner.Summarize("ok", "", 0);
        Assert.Contains("exit=0", summary);
    }

    [Fact]
    public async Task RunCommands_fails_on_nonzero_exit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "jz-cli-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var cmd = OperatingSystem.IsWindows() ? "exit /b 1" : "false";
            var runs = await VerificationRunner.RunCommandsAsync(dir, [cmd]);
            Assert.Single(runs);
            Assert.False(runs[0].Passed);
            Assert.Equal(1, runs[0].ExitCode);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
