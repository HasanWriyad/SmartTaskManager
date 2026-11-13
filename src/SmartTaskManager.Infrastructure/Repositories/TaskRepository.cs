using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Paging;
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

    public Task<PagedResult<TaskListItemDto>> ListByProjectAsync(
        Guid projectId,
        TaskListQuery query,
        CancellationToken cancellationToken)
    {
        var tasks = Filter(db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId), query);
        var direction = query.SortDirection;

        var ordered = query switch
        {
            { SortBy: null } => tasks // default: highest priority, then earliest due date (none last)
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.DueDate == null)
                .ThenBy(t => t.DueDate),
            _ when query.IsSortField(TaskSortFields.Title) => tasks.OrderByDirection(t => t.Title, direction),
            _ when query.IsSortField(TaskSortFields.Priority) => tasks.OrderByDirection(t => t.Priority, direction),
            _ when query.IsSortField(TaskSortFields.Status) => tasks.OrderByDirection(t => t.Status, direction),
            _ when query.IsSortField(TaskSortFields.DueDate) => tasks
                .OrderBy(t => t.DueDate == null) // tasks without a due date last, in both directions
                .ThenByDirection(t => t.DueDate, direction),
            _ => tasks.OrderByDirection(t => t.CreatedAt, direction), // createdAt (validator allows nothing else)
        };

        return ordered
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(TaskAccess.ToListItem)
            .ToPagedResultAsync(query, cancellationToken);
    }

    private static IQueryable<TaskItem> Filter(IQueryable<TaskItem> tasks, TaskListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            tasks = tasks.Where(t => t.Title.Contains(term) || (t.Description != null && t.Description.Contains(term)));
        }

        if (query.Status is { } status)
        {
            tasks = tasks.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tasks = tasks.Where(t => t.Priority == priority);
        }

        if (query.AssigneeId is { } assigneeId)
        {
            tasks = tasks.Where(t => t.AssigneeId == assigneeId);
        }

        if (query.DueFrom is { } dueFrom)
        {
            tasks = tasks.Where(t => t.DueDate != null && t.DueDate >= dueFrom);
        }

        if (query.DueTo is { } dueTo)
        {
            tasks = tasks.Where(t => t.DueDate != null && t.DueDate <= dueTo);
        }

        return tasks;
    }

    public Task<List<TaskItem>> GetAssignedInProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        db.Tasks
            .Where(t => t.ProjectId == projectId && t.AssigneeId == userId)
            .ToListAsync(cancellationToken);

    public void Add(TaskItem task) => db.Tasks.Add(task);

    public void Remove(TaskItem task) => db.Tasks.Remove(task);
}
