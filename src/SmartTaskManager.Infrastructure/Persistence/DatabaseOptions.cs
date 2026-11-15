namespace SmartTaskManager.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations at startup. Intended for local development.</summary>
    public bool ApplyMigrationsOnStartup { get; init; }
}
