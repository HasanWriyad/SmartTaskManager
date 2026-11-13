using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Common;

namespace SmartTaskManager.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Turns deletes of <see cref="ISoftDeletable"/> entities into updates that set the soft-delete fields.
/// It applies no business rules: which entities are deleted together (e.g. a project and its tasks) is
/// decided by the application services.
/// </summary>
internal sealed class SoftDeleteInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplySoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplySoftDelete(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var deleted = context.ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        if (deleted.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var entry in deleted)
        {
            // Write only the soft-delete columns (plus UpdatedAt from the audit interceptor), so a
            // delete never overwrites other columns with values loaded earlier.
            entry.State = EntityState.Unchanged;
            entry.Entity.MarkDeleted(now, currentUser.UserId);
            entry.Property(nameof(ISoftDeletable.IsDeleted)).IsModified = true;
            entry.Property(nameof(ISoftDeletable.DeletedAt)).IsModified = true;
            entry.Property(nameof(ISoftDeletable.DeletedById)).IsModified = true;
        }
    }
}
