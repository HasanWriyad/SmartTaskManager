namespace SmartTaskManager.Infrastructure.Persistence.Seed;

/// <summary>
/// Credentials for the initial Admin user. Supply them through user-secrets or environment variables
/// (e.g. <c>SeedAdmin__Email</c>, <c>SeedAdmin__Password</c>); never in appsettings.json.
/// </summary>
public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string? Email { get; init; }

    public string? Password { get; init; }

    public string FullName { get; init; } = "System Administrator";
}
