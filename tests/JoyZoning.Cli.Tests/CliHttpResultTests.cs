using System.Net;
using JoyZoning.Cli;
using Xunit;

namespace JoyZoning.Cli.Tests;

public class CliHttpResultTests
{
    [Fact]
    public void FromResponse_parses_api_error_envelope()
    {
        var result = CliHttpResult.FromResponse(
            HttpStatusCode.Conflict,
            """{"error":"lease_conflict","message":"Critical card requires humanApprovedCritical=true."}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("lease_conflict", result.Error);
        Assert.Contains("Critical", result.Message);
    }

    [Fact]
    public void NetworkError_uses_exit_code_2_classification()
    {
        var result = CliHttpResult.NetworkError("Connection refused");
        Assert.True(result.IsNetworkError);
        Assert.Equal("network_error", result.Error);
    }
}
