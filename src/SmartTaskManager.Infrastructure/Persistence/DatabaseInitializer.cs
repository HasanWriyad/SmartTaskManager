using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartTaskManager.Infrastructure.Persistence.Seed;

namespace SmartTaskManager.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Optionally applies migrations, seeds the initial Admin user if none exists, and, only when
    /// <paramref name="isDevelopment"/> is true, seeds demo data into an empty database.
    /// </summary>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        bool isDevelopment,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        if (provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
        {
            await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        }

        await provider.GetRequiredService<AdminSeeder>().SeedAsync(cancellationToken);
        await provider.GetRequiredService<DemoDataSeeder>().SeedAsync(isDevelopment, cancellationToken);
    }
}
