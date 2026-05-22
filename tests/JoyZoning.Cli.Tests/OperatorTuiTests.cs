using JoyZoning.Cli.Tui;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class OperatorTuiTests
{
    [Theory]
    [InlineData("/help", "help")]
    [InlineData("/h", "help")]
    [InlineData("/dispatch --approve-critical", "dispatch")]
    [InlineData("/run --poll 5", "dispatch")]
    [InlineData("/hermes", "hermes")]
    [InlineData("/ws changed", "workspace")]
    [InlineData("/workspace tree", "workspace")]
    public void Slash_resolve_maps_aliases(string input, string expected)
    {
        Assert.True(OperatorSlashRegistry.TryResolve(input, out var canonical, out _));
        Assert.Equal(expected, canonical);
    }

    [Fact]
    public void Slash_completion_includes_prefix()
    {
        var dispatchHits = OperatorSlashRegistry.CompletionCandidates("/dis");
        Assert.Contains("/dispatch", dispatchHits);

        var doctorHits = OperatorSlashRegistry.CompletionCandidates("/doc");
        Assert.Contains("/doctor", doctorHits);
    }

    [Fact]
    public void ShouldLaunchInteractive_respects_argv_and_no_tui_env()
    {
        Assert.False(OperatorTuiRunner.ShouldLaunchInteractive(["doctor"]));

        var prev = Environment.GetEnvironmentVariable(OperatorTuiRunner.NoTuiEnv);
        try
        {
            Environment.SetEnvironmentVariable(OperatorTuiRunner.NoTuiEnv, "1");
            Assert.False(OperatorTuiRunner.ShouldLaunchInteractive([]));
        }
        finally
        {
            Environment.SetEnvironmentVariable(OperatorTuiRunner.NoTuiEnv, prev);
        }

        // Bare `jz` auto-launches TUI only on a real TTY (xUnit redirects stdin/out).
        if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
            Assert.True(OperatorTuiRunner.ShouldLaunchInteractive([]));
    }
}
