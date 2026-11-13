using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Application.Features.Tasks;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

/// <summary>
/// Soft-deleted tasks are invisible to every method here. Callers check project visibility first;
/// these methods only scope by project id.
/// </summary>
public interface ITaskRepository
{
    /// <summary>Tracked load of a task in the given project, for updates.</summary>
    Task<TaskItem?> GetInProjectAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);

    Task<TaskDetailsDto?> GetDetailsAsync(Guid projectId, Guid taskId, Viewer viewer, CancellationToken cancellationToken);

    Task<PagedResult<TaskListItemDto>> ListByProjectAsync(Guid projectId, TaskListQuery query, CancellationToken cancellationToken);

    /// <summary>Tracked load of the project's tasks assigned to the user.</summary>
    Task<List<TaskItem>> GetAssignedInProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);

    void Add(TaskItem task);

    /// <summary>Marks for deletion; the soft-delete interceptor turns it into a soft delete.</summary>
    void Remove(TaskItem task);
}
