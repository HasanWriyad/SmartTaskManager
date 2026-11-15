using System.Globalization;
using Serilog;
using SmartTaskManager.Api.Configuration;
using SmartTaskManager.Api.ErrorHandling;
using SmartTaskManager.Api.Extensions;
using SmartTaskManager.Api.Health;
using SmartTaskManager.Api.RateLimiting;
using SmartTaskManager.Application;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Infrastructure;
using SmartTaskManager.Infrastructure.Persistence;

// Bootstrap logger captures startup failures before configuration is loaded.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Exceeding the limit returns 413 (as ApiResponse via the exception handler). Kestrel is configured
    // before the container is built, so the options are bound here as well as registered (ApiExtensions).
    var requestLimits = builder.Configuration.GetSection(RequestLimitsOptions.SectionName).Get<RequestLimitsOptions>()
        ?? new RequestLimitsOptions();
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = requestLimits.MaxRequestBodyBytes);

    builder.Services.AddSerilog((services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddProblemDetails();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorizationPolicies();
    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddApiRateLimiting(builder.Configuration);
    builder.Services.AddApiHealthChecks();

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping);

    // Resolve once so the startup log states which AI implementation is active (provider or fallback).
    _ = app.Services.GetRequiredService<IAiDescriptionService>();

    // Outermost, so the logged status code is the one the client actually receives.
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    // Gives empty framework responses (401, 403, 404, 405, ...) an ApiResponse body.
    app.UseStatusCodePages(context => ErrorResponseWriter.WriteAsync(
        context.HttpContext,
        context.HttpContext.Response.StatusCode,
        ErrorResponseWriter.MessageFor(context.HttpContext.Response.StatusCode)));

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwaggerDocumentation();
    }

    app.UseCors(ApiExtensions.FrontendCorsPolicy);
    app.UseAuthentication();
    // After authentication so the AI policy can partition by user id; before authorization so
    // floods are rejected cheaply.
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();
    app.MapApiHealthChecks();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown on purpose by EF Core design-time tooling; not an error.
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
