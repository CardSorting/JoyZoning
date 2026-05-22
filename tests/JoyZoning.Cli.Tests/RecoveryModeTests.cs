using JoyZoning.Cli;
using JoyZoning.Domain.Enums;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class RecoveryModeTests
{
    [Theory]
    [InlineData("reopen", LeaseRecoveryMode.ReopenBlocked)]
    [InlineData("reattach", LeaseRecoveryMode.ReattachWorktree)]
    [InlineData("replace", LeaseRecoveryMode.ReplacementLease)]
    [InlineData("2", LeaseRecoveryMode.ReplacementLease)]
    public void ParseRecoveryMode_accepts_names(string mode, LeaseRecoveryMode expected) =>
        Assert.Equal(expected, OperatorWorkflows.ParseRecoveryMode(mode));

    [Fact]
    public void Replace_recovery_requires_yes_in_dispatcher()
    {
        var args = CliArgs.Parse(["task", "recover", Guid.NewGuid().ToString(), "--mode", "replace"]);
        Assert.Throws<CliUsageException>(() =>
            CliSafety.RequireYes(args, "recover with replacement lease"));
    }
}
