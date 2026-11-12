using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Infrastructure.Auth;
using SmartTaskManager.Infrastructure.Persistence;
using SmartTaskManager.Infrastructure.Persistence.Interceptors;
using SmartTaskManager.Infrastructure.Persistence.Seed;

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

        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(SqlServerCompatibilityLevel))
            // Order matters: soft delete turns Deleted into Modified before audit stamps UpdatedAt.
            .AddInterceptors(
                sp.GetRequiredService<SoftDeleteInterceptor>(),
                sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<AdminSeeder>();

        return services;
    }
}
