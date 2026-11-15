using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SmartTaskManager.Api.ErrorHandling;
using SmartTaskManager.Infrastructure.Auth;

namespace SmartTaskManager.Api.RateLimiting;

/// <summary>Requests allowed per minute (fixed one-minute windows).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Every request, per client IP.</summary>
    [Range(1, 100_000)]
    public int GlobalPerMinute { get; init; } = 100;

    /// <summary>Login and register, per client IP (brute-force protection).</summary>
    [Range(1, 100_000)]
    public int AuthPerMinute { get; init; } = 5;

    /// <summary>AI endpoint, per signed-in user (it calls a paid external service).</summary>
    [Range(1, 100_000)]
    public int AiPerMinute { get; init; } = 10;
}

/// <summary>Endpoint rate-limit policy names, used with [EnableRateLimiting].</summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Ai = "ai";
}

internal static partial class RateLimitingSetup
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Applies to every request that reaches the limiter (all API endpoints; /health opts out).
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                FixedWindow(ClientIp(context), limits.GlobalPerMinute));

            options.AddPolicy(RateLimitPolicies.Auth, context =>
                FixedWindow(ClientIp(context), limits.AuthPerMinute));

            // Per user, so colleagues behind one office IP do not share a budget; anonymous callers
            // never reach the AI action (it requires a role) but are partitioned by IP just in case.
            options.AddPolicy(RateLimitPolicies.Ai, context =>
                FixedWindow(UserOrIp(context), limits.AiPerMinute));

            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = Window,
            QueueLimit = 0, // reject immediately rather than holding requests open
        });

    /// <summary>
    /// The connection's remote address. Behind a reverse proxy, configure ForwardedHeaders first,
    /// otherwise every client shares the proxy's IP.
    /// </summary>
    private static string ClientIp(HttpContext context) =>
        "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private static string UserOrIp(HttpContext context) =>
        context.User.FindFirst(AppClaimTypes.UserId)?.Value is { } userId ? "user:" + userId : ClientIp(context);

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        var message = "Too many requests. Please try again later.";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            message = $"Too many requests. Please try again in {seconds} seconds.";
        }

        var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SmartTaskManager.Api.RateLimiting");
        LogRejected(logger, http.Request.Method, http.Request.Path);

        await ErrorResponseWriter.WriteAsync(http, StatusCodes.Status429TooManyRequests, message);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit exceeded for {Method} {Path}")]
    private static partial void LogRejected(ILogger logger, string method, PathString path);
}
