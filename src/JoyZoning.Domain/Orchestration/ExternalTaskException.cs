namespace JoyZoning.Domain.Orchestration;

public sealed class ExternalTaskException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public ExternalTaskException(string code, string message, int statusCode = 409)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public static ExternalTaskException NotFound(string message) =>
        new("external_task_not_found", message, 404);

    public static ExternalTaskException Conflict(string message) =>
        new("external_task_conflict", message, 409);

    public static ExternalTaskException BadRequest(string message) =>
        new("external_task_invalid_request", message, 400);

    public static ExternalTaskException Forbidden(string message) =>
        new("external_task_forbidden", message, 403);
}
