namespace SmartTaskManager.Application.Common.Exceptions;

/// <summary>
/// An external dependency (e.g. the AI provider) failed. Carries the HTTP status the API should
/// return (429, 502, 503 or 504) and, for rate limits, how long the client should wait.
/// The message is safe to show to users.
/// </summary>
public sealed class ExternalServiceException(int statusCode, string message, TimeSpan? retryAfter = null)
    : AppException(message)
{
    public int StatusCode { get; } = statusCode;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
