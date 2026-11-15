namespace SmartTaskManager.Application.Common;

/// <summary>Envelope used for every API response, successful or not.</summary>
/// <typeparam name="T">Payload type. Responses without a payload use <see cref="object"/> and a null <see cref="Data"/>.</typeparam>
public sealed record ApiResponse<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string? Message { get; init; }

    /// <summary>Validation errors keyed by field name; only present on validation failures.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>Correlates an error response with server logs; only present on failures.</summary>
    public string? TraceId { get; init; }
}

/// <summary>Factory methods for <see cref="ApiResponse{T}"/>.</summary>
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<object> Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse<object> Fail(
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? traceId = null) =>
        new() { Success = false, Message = message, Errors = errors, TraceId = traceId };
}
