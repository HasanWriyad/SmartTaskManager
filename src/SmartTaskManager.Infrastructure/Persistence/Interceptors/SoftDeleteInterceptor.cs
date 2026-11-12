using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Common;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Turns deletes of <see cref="ISoftDeletable"/> entities into updates that set the soft-delete fields.
/// Deleting a project also soft-deletes its tasks; everything is written in the same SaveChanges transaction.
/// </summary>
internal sealed class SoftDeleteInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            var projectIds = GetDeletedProjectIds(context);
            if (projectIds.Count > 0)
            {
                context.Set<TaskItem>().RemoveRange(TasksOf(context, projectIds).ToList());
            }

            ApplySoftDelete(context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var projectIds = GetDeletedProjectIds(context);
            if (projectIds.Count > 0)
            {
                context.Set<TaskItem>().RemoveRange(await TasksOf(context, projectIds).ToListAsync(cancellationToken));
            }

            ApplySoftDelete(context);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static List<Guid> GetDeletedProjectIds(DbContext context) =>
        [.. context.ChangeTracker.Entries<Project>()
            .Where(e => e.State == EntityState.Deleted)
            .Select(e => e.Entity.Id)];

    // The soft-delete filter already excludes tasks that were deleted earlier.
    private static IQueryable<TaskItem> TasksOf(DbContext context, List<Guid> projectIds) =>
        context.Set<TaskItem>().Where(t => projectIds.Contains(t.ProjectId));

    private void ApplySoftDelete(DbContext context)
    {
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
            entry.State = EntityState.Modified;
            entry.Entity.MarkDeleted(now, currentUser.UserId);
        }
    }
}
