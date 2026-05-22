namespace JoyZoning.Domain.Orchestration;

/// <summary>Structured orchestration failure mapped to HTTP status in the control plane.</summary>
public sealed class LeaseOrchestrationException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public LeaseOrchestrationException(string code, string message, int statusCode = 409)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public static LeaseOrchestrationException NotFound(string message) =>
        new("lease_not_found", message, 404);

    public static LeaseOrchestrationException Conflict(string message) =>
        new("lease_conflict", message, 409);

    public static LeaseOrchestrationException BadRequest(string message) =>
        new("lease_invalid_request", message, 400);

    public static LeaseOrchestrationException Forbidden(string message) =>
        new("lease_forbidden", message, 403);
}
