using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using JoyZoning.Domain.Configuration;

namespace JoyZoning.ControlPlane.Services;

/// <summary>
/// Guards internal-only routes (Hermes observation ingest, agent lease callbacks).
/// When InternalToken is configured, requests without a matching header are rejected.
/// </summary>
public class HabitatInternalAuth
{
    public const string TokenHeader = "X-JoyZoning-Internal-Token";

    private readonly ControlPlaneOptions _options;
    private readonly IHostEnvironment _environment;

    public HabitatInternalAuth(IOptions<ControlPlaneOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.InternalToken);

    public IResult? RequireInternal(HttpRequest request)
    {
        if (_environment.IsEnvironment("Testing"))
            return null;

        var expected = _options.InternalToken;
        if (string.IsNullOrWhiteSpace(expected))
        {
            if (!_environment.IsDevelopment())
            {
                return Results.Json(
                    new
                    {
                        error = "internal_token_required",
                        message = "ControlPlane:InternalToken must be set in non-development environments.",
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return null;
        }

        if (!request.Headers.TryGetValue(TokenHeader, out var provided)
            || !string.Equals(provided.ToString(), expected, StringComparison.Ordinal))
        {
            return Results.Json(
                new
                {
                    error = "forbidden",
                    message = "Invalid or missing internal token.",
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }
}
