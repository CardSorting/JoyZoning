using JoyZoning.Domain.Orchestration;

namespace JoyZoning.ControlPlane.Services;

internal static class LeaseApiResults
{
    public static IResult FromException(Exception ex) =>
        ex switch
        {
            LeaseOrchestrationException lease => Results.Json(
                new { error = lease.Code, message = lease.Message },
                statusCode: lease.StatusCode),
            InvalidOperationException invalid => Results.Json(
                new { error = "invalid_operation", message = invalid.Message },
                statusCode: 409),
            ArgumentException arg => Results.Json(
                new { error = "invalid_argument", message = arg.Message },
                statusCode: 400),
            _ => Results.Json(
                new { error = "internal_error", message = "Request could not be completed." },
                statusCode: 500),
        };
}