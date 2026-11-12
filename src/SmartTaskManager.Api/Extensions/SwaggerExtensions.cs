using Microsoft.OpenApi;
using SmartTaskManager.Api.Swagger;

namespace SmartTaskManager.Api.Extensions;

internal static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Smart Task Manager API",
                Version = "v1",
                Description = "Projects, tasks, dashboard and AI-assisted task descriptions. "
                    + "Every response is wrapped in ApiResponse<T>.",
            });

            options.AddSecurityDefinition(AuthorizeOperationFilter.SchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token returned by /api/auth/login (without the 'Bearer ' prefix).",
            });
            options.OperationFilter<AuthorizeOperationFilter>();

            var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            if (File.Exists(xmlFile))
            {
                options.IncludeXmlComments(xmlFile);
            }
        });

        return services;
    }

    public static WebApplication UseSwaggerDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Task Manager API v1");
            options.DocumentTitle = "Smart Task Manager API";
        });

        return app;
    }
}
