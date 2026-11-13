using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Infrastructure.Persistence;

namespace SmartTaskManager.Api.Health;

public sealed record HealthReportDto(string Status, double TotalDurationMs, IReadOnlyList<HealthCheckDto> Checks);

public sealed record HealthCheckDto(string Name, string Status, double DurationMs);

internal static class HealthCheckSetup
{
    public const string Path = "/health";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        // Opens a connection through EF Core (CanConnectAsync) to verify the database is reachable.
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        return services;
    }

    /// <summary>
    /// Anonymous and exempt from rate limiting so load balancers and monitors can poll it.
    /// Returns 200 when healthy, 503 otherwise. Exception details are never included.
    /// </summary>
    public static IEndpointConventionBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHealthChecks(Path, new HealthCheckOptions { ResponseWriter = WriteResponseAsync })
            .AllowAnonymous()
            .DisableRateLimiting();

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        var dto = new HealthReportDto(
            report.Status.ToString(),
            Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            [.. report.Entries.Select(e => new HealthCheckDto(e.Key, e.Value.Status.ToString(), Math.Round(e.Value.Duration.TotalMilliseconds, 1)))]);

        var healthy = report.Status == HealthStatus.Healthy;
        var body = new ApiResponse<HealthReportDto>
        {
            Success = healthy,
            Data = dto,
            Message = healthy ? "Healthy" : "One or more health checks failed.",
        };
        return context.Response.WriteAsJsonAsync(body, context.RequestAborted);
    }
}
