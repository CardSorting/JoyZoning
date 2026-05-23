using System.Net;

namespace JoyZoning.Agents.Hermes;

public sealed class HermesApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ResponseBody { get; }

    public HermesApiException(HttpStatusCode statusCode, string message, string? responseBody = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
}
