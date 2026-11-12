using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SmartTaskManager.Api.ErrorHandling;
using SmartTaskManager.Api.Filters;
using SmartTaskManager.Api.Services;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;

namespace SmartTaskManager.Api.Extensions;

internal static class ApiExtensions
{
    public const string FrontendCorsPolicy = "Frontend";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddExceptionHandler<GlobalExceptionHandler>();

        services
            .AddControllers(o =>
            {
                o.Filters.Add<ValidationFilter>();
                // FluentValidation is the single source of input rules; don't let non-nullable
                // properties add their own implicit "field is required" errors.
                o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                // Don't echo System.Text.Json parser internals back to clients.
                o.AllowInputFormatterExceptionMessages = false;
            })
            .ConfigureApiBehaviorOptions(o =>
            {
                o.InvalidModelStateResponseFactory = InvalidModelStateResponse;
                // Leave bodiless 4xx results (e.g. 415) empty so UseStatusCodePages writes them as
                // ApiResponse, instead of [ApiController] turning them into ProblemDetails.
                o.SuppressMapClientErrors = true;
            });

        // Responses written outside MVC (error handler, status code pages) use the same enum format.
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddFrontendCors(configuration);

        return services;
    }

    /// <summary>
    /// Model-binding failures (malformed JSON, wrong types) return the same ApiResponse shape as
    /// FluentValidation failures instead of the default ProblemDetails.
    /// </summary>
    private static BadRequestObjectResult InvalidModelStateResponse(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value is { Errors.Count: > 0 })
            .ToDictionary(
                // An empty key means the body as a whole (e.g. missing body).
                e => string.IsNullOrEmpty(e.Key) ? "body" : e.Key,
                e => e.Value!.Errors
                    .Select(err => string.IsNullOrEmpty(err.ErrorMessage) ? "The value is invalid." : err.ErrorMessage)
                    .ToArray());

        var body = ApiResponse.Fail(
            "One or more validation errors occurred.",
            errors,
            ErrorResponseWriter.GetTraceId(context.HttpContext));

        return new BadRequestObjectResult(body);
    }

    /// <summary>
    /// Allows the configured frontend origins. Credentials are allowed because the refresh token
    /// travels in an httpOnly cookie; that requires explicit origins (no wildcard).
    /// In development the Angular proxy makes calls same-origin, so CORS is only needed when deployed separately.
    /// </summary>
    private static void AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
    }
}
