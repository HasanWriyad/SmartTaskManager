using System.Globalization;
using Serilog;
using SmartTaskManager.Api.ErrorHandling;
using SmartTaskManager.Api.Extensions;
using SmartTaskManager.Application;
using SmartTaskManager.Infrastructure;
using SmartTaskManager.Infrastructure.Persistence;

// Bootstrap logger captures startup failures before configuration is loaded.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

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

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping);

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
    app.UseAuthorization();

    app.MapControllers();

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
