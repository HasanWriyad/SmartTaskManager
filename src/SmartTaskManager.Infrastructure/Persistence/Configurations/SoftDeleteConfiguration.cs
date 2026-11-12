using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTaskManager.Domain.Common;

namespace SmartTaskManager.Infrastructure.Persistence.Configurations;

internal static class SoftDeleteConfiguration
{
    /// <summary>Adds the named soft-delete query filter that hides rows where IsDeleted is set.</summary>
    public static void ConfigureSoftDelete<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : SoftDeletableEntity =>
        builder.HasQueryFilter(QueryFilters.SoftDelete, e => !e.IsDeleted);
}
