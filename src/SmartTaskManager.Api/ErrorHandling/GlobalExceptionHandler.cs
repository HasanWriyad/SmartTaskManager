using Microsoft.AspNetCore.Diagnostics;
using SmartTaskManager.Application.Common.Exceptions;
using SmartTaskManager.Domain.Common;

namespace SmartTaskManager.Api.ErrorHandling;

/// <summary>
/// Maps exceptions to HTTP status codes and <see cref="Application.Common.ApiResponse{T}"/> bodies.
/// Expected errors return their message; unexpected ones return a generic message and are logged in full.
/// </summary>
internal sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string UnexpectedErrorMessage = "An unexpected error occurred.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The client went away; there is nobody to respond to.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogRequestAborted(logger, httpContext.Request.Method, httpContext.Request.Path);
            return true;
        }

        var (statusCode, message, errors) = exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, e.Message, e.Errors),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, e.Message, null),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Message, null),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Message, null),
            ConflictException e => (StatusCodes.Status409Conflict, e.Message, null),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Message, null),
            BadHttpRequestException e => (e.StatusCode, ErrorResponseWriter.MessageFor(e.StatusCode), null),
            _ => (StatusCodes.Status500InternalServerError, UnexpectedErrorMessage, (IReadOnlyDictionary<string, string[]>?)null),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        else if (logger.IsEnabled(LogLevel.Information))
        {
            var exceptionType = exception.GetType().Name;
            LogHandledException(logger, exceptionType, statusCode, httpContext.Request.Method, httpContext.Request.Path);
        }

        await ErrorResponseWriter.WriteAsync(httpContext, statusCode, message, errors);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ExceptionType} mapped to {StatusCode} for {Method} {Path}")]
    private static partial void LogHandledException(ILogger logger, string exceptionType, int statusCode, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request aborted by client: {Method} {Path}")]
    private static partial void LogRequestAborted(ILogger logger, string method, PathString path);
}
