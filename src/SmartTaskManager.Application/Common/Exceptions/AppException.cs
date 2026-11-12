namespace SmartTaskManager.Application.Common.Exceptions;

/// <summary>
/// Base for expected application errors. The API maps each subtype to an HTTP status code;
/// the message is safe to return to the client.
/// </summary>
public abstract class AppException(string message) : Exception(message);

/// <summary>Request input is invalid (HTTP 400).</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>Credentials or a refresh token were rejected (HTTP 401).</summary>
public sealed class UnauthorizedException(string message = "Authentication failed.") : AppException(message);

/// <summary>The caller can see the resource but may not perform the action (HTTP 403).</summary>
public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(message);

/// <summary>The resource does not exist or is not visible to the caller (HTTP 404).</summary>
public sealed class NotFoundException(string resource, object key)
    : AppException($"{resource} '{key}' was not found.");

/// <summary>The request conflicts with existing state, e.g. a duplicate name (HTTP 409).</summary>
public sealed class ConflictException(string message) : AppException(message);
