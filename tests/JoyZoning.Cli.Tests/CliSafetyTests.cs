using JoyZoning.Cli;
using JoyZoning.Domain.Enums;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class CliSafetyTests
{
    [Fact]
    public void Merge_requires_yes()
    {
        var args = CliArgs.Parse(["task", "merge", Guid.NewGuid().ToString()]);
        var ex = Assert.Throws<CliUsageException>(() => CliSafety.RequireYes(args, "task merge"));
        Assert.Contains("--yes", ex.Message);
    }

    [Fact]
    public void ForbidDirectComplete_blocks_complete_status()
    {
        var ex = Assert.Throws<CliUsageException>(() =>
            CliSafety.ForbidDirectComplete(WorkTaskStatus.Complete));
        Assert.Contains("task complete", ex.Message);
    }

    [Fact]
    public void Raw_delete_requires_yes()
    {
        var args = CliArgs.Parse(["raw", "DELETE", "api/tasks/x"]);
        Assert.Throws<CliUsageException>(() => CliSafety.GuardRawRequest(args, "DELETE", "api/tasks/x"));
    }
}
