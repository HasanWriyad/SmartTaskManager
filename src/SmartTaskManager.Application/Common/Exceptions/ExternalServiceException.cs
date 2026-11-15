namespace SmartTaskManager.Application.Common.Exceptions;

/// <summary>How an external dependency (e.g. the AI provider) failed. The API layer maps it to HTTP.</summary>
public enum ExternalServiceFailure
{
    /// <summary>The dependency is throttling us; the caller should wait (see <see cref="ExternalServiceException.RetryAfter"/>).</summary>
    RateLimited,

    /// <summary>The dependency is down, overloaded, misconfigured or unreachable.</summary>
    Unavailable,

    /// <summary>The dependency did not answer within the configured time.</summary>
    Timeout,

    /// <summary>The dependency answered, but the result is unusable (empty, truncated, filtered, malformed).</summary>
    InvalidResponse,
}

/// <summary>An external dependency failed. The message is safe to show to users.</summary>
public sealed class ExternalServiceException(ExternalServiceFailure failure, string message, TimeSpan? retryAfter = null)
    : AppException(message)
{
    public ExternalServiceFailure Failure { get; } = failure;

    /// <summary>Only set for <see cref="ExternalServiceFailure.RateLimited"/>, when the dependency said how long to wait.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
