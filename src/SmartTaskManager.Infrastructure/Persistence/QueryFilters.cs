namespace SmartTaskManager.Infrastructure.Persistence;

/// <summary>
/// Names of the global query filters (EF Core 10 named filters).
/// Disable one selectively with <c>IgnoreQueryFilters([QueryFilters.SoftDelete])</c>.
/// </summary>
public static class QueryFilters
{
    public const string SoftDelete = nameof(SoftDelete);
}
