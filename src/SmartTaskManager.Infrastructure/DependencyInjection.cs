using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Infrastructure.Ai;
using SmartTaskManager.Infrastructure.Auth;
using SmartTaskManager.Infrastructure.Persistence;
using SmartTaskManager.Infrastructure.Persistence.Interceptors;
using SmartTaskManager.Infrastructure.Persistence.Seed;
using SmartTaskManager.Infrastructure.Repositories;

namespace SmartTaskManager.Infrastructure;

public static class DependencyInjection
{
    /// <summary>SQL Server 2019 (LocalDB) compatibility level.</summary>
    private const int SqlServerCompatibilityLevel = 150;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<SeedAdminOptions>().Bind(configuration.GetSection(SeedAdminOptions.SectionName));
        services.AddOptions<SeedDemoOptions>().Bind(configuration.GetSection(SeedDemoOptions.SectionName));
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(SqlServerCompatibilityLevel))
            // Order matters: soft delete turns Deleted into Modified before audit stamps UpdatedAt.
            .AddInterceptors(
                sp.GetRequiredService<SoftDeleteInterceptor>(),
                sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        services.AddOptions<AiSettings>()
            .Bind(configuration.GetSection(AiSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var aiSettings = configuration.GetSection(AiSettings.SectionName).Get<AiSettings>() ?? new AiSettings();
        if (aiSettings.HasApiKey)
        {
            services.AddSingleton<IAiDescriptionService, OpenAiCompatibleDescriptionService>();
        }
        else
        {
            services.AddSingleton<IAiDescriptionService, FallbackDescriptionService>();
        }

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<AdminSeeder>();
        services.AddScoped<DemoDataSeeder>();

        return services;
    }
}
