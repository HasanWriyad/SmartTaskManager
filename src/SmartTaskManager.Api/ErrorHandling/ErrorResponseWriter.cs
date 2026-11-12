using System.Diagnostics;
using SmartTaskManager.Application.Common;

namespace SmartTaskManager.Api.ErrorHandling;

/// <summary>Writes error responses as <see cref="ApiResponse{T}"/> so every failure has the same shape.</summary>
internal static class ErrorResponseWriter
{
    public static Task WriteAsync(
        HttpContext context,
        int statusCode,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        var body = ApiResponse.Fail(message, errors, GetTraceId(context));
        return context.Response.WriteAsJsonAsync(body, context.RequestAborted);
    }

    public static string GetTraceId(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;

    /// <summary>Default message for status codes produced by the framework without a body (401, 403, 404, ...).</summary>
    public static string MessageFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "The request is invalid.",
        StatusCodes.Status401Unauthorized => "Authentication is required.",
        StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
        StatusCodes.Status404NotFound => "The requested resource was not found.",
        StatusCodes.Status405MethodNotAllowed => "The HTTP method is not allowed for this resource.",
        StatusCodes.Status415UnsupportedMediaType => "The request content type is not supported.",
        StatusCodes.Status429TooManyRequests => "Too many requests. Please try again later.",
        _ => "The request could not be processed.",
    };
}
