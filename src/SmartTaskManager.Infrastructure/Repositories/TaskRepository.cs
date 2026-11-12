using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Tasks;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Infrastructure.Persistence;

namespace SmartTaskManager.Infrastructure.Repositories;

// The soft-delete query filter hides deleted tasks everywhere.
internal sealed class TaskRepository(AppDbContext db) : ITaskRepository
{
    public Task<TaskItem?> GetInProjectAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        db.Tasks.SingleOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId, cancellationToken);

    public Task<TaskDetailsDto?> GetDetailsAsync(Guid projectId, Guid taskId, Viewer viewer, CancellationToken cancellationToken) =>
        db.Tasks
            .AsNoTracking()
            .Where(t => t.Id == taskId && t.ProjectId == projectId)
            .Select(TaskAccess.ToDetails(viewer))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskListItemDto>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await db.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate == null) // tasks without a due date last
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(TaskAccess.ToListItem)
            .ToListAsync(cancellationToken);

    public Task<List<TaskItem>> GetAssignedInProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        db.Tasks
            .Where(t => t.ProjectId == projectId && t.AssigneeId == userId)
            .ToListAsync(cancellationToken);

    public void Add(TaskItem task) => db.Tasks.Add(task);

    public void Remove(TaskItem task) => db.Tasks.Remove(task);
}
