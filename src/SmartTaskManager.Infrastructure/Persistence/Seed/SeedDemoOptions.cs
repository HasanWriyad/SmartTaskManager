namespace SmartTaskManager.Infrastructure.Persistence.Seed;

/// <summary>
/// Development-only demo data (see <see cref="DemoDataSeeder"/>). The shared password for the demo
/// users comes from user-secrets or an environment variable (<c>SeedDemo:Password</c>); never from
/// appsettings.json.
/// </summary>
public sealed class SeedDemoOptions
{
    public const string SectionName = "SeedDemo";

    /// <summary>Set in appsettings.Development.json; the seeder also requires the Development environment.</summary>
    public bool Enabled { get; init; }

    /// <summary>Password for every demo user.</summary>
    public string? Password { get; init; }
}
