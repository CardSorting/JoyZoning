using System.Text.Json;
using JoyZoning.Cli;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class CliJsonTests
{
    [Fact]
    public void ExtractField_supports_dotted_paths()
    {
        var json = JsonSerializer.SerializeToElement(new { id = "abc-123", status = 2 });
        Assert.Equal("abc-123", CliJson.ExtractField(json, "id"));
        Assert.Equal("2", CliJson.ExtractField(json, "status"));
    }
}
