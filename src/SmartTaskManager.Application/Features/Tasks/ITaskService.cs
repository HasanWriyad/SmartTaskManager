using SmartTaskManager.Application.Common.Paging;

namespace SmartTaskManager.Application.Features.Tasks;

/// <summary>
/// Tasks within a project. The project must be visible to the caller, and the task must belong to it,
/// otherwise the result is 404. Visible tasks the caller may not change give 403.
/// </summary>
public interface ITaskService
{
    Task<PagedResult<TaskListItemDto>> ListAsync(Guid projectId, TaskListQuery query, CancellationToken cancellationToken);

    /// <summary>Tasks across every project the caller can see (Admin: all projects).</summary>
    Task<PagedResult<TaskListItemDto>> ListAllAsync(AllTasksListQuery query, CancellationToken cancellationToken);

    Task<TaskDetailsDto> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);

    Task<TaskDetailsDto> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken cancellationToken);

    Task<TaskDetailsDto> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken);

    Task<TaskDetailsDto> AssignAsync(Guid projectId, Guid taskId, AssignTaskRequest request, CancellationToken cancellationToken);

    Task<TaskDetailsDto> UnassignAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Owner/Admin may make any allowed transition; any active assignee only on their own task and
    /// not reopening a cancelled task. A cancelled task can only go back to To Do (422 otherwise).
    /// </summary>
    Task<TaskDetailsDto> ChangeStatusAsync(Guid projectId, Guid taskId, UpdateTaskStatusRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken);
}
