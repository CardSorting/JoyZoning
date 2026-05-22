using JoyZoning.Cli;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class CliArgsTests
{
    [Fact]
    public void Parse_extracts_global_flags_and_positionals()
    {
        var args = CliArgs.Parse(["--pretty", "--yes", "--field", ".id", "task", "list"]);
        Assert.True(args.PrettyJson);
        Assert.True(args.Yes);
        Assert.Equal(".id", args.FieldPath);
        Assert.Equal(["task", "list"], args.Positionals);
    }

    [Fact]
    public void ApproveCritical_requires_explicit_flag()
    {
        var args = CliArgs.Parse(["task", "dispatch", Guid.NewGuid().ToString()]);
        Assert.False(args.ApproveCritical);
        Assert.True(CliArgs.Parse(["--approve-critical", "task", "dispatch", "x"]).ApproveCritical);
    }

    [Fact]
    public void OptAll_collects_multiple_cmd()
    {
        var args = CliArgs.Parse(["task", "verify", "id", "--cmd", "dotnet build", "--cmd", "dotnet test"]);
        Assert.Equal(2, args.OptAll("--cmd").Count);
    }
}
