using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartTaskManager.Infrastructure.Persistence.Seed;

namespace SmartTaskManager.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Optionally applies migrations, then seeds the initial Admin user if none exists.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        if (provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
        {
            await provider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        }

        await provider.GetRequiredService<AdminSeeder>().SeedAsync(cancellationToken);
    }
}
