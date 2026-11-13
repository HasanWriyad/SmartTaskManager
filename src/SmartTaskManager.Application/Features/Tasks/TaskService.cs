using Microsoft.Extensions.Logging;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Exceptions;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Tasks;

internal sealed partial class TaskService(
    ITaskRepository tasks,
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<TaskService> logger) : ITaskService
{
    private const string AssigneeNotMember = "The assignee must be an active member of the project.";

    public async Task<PagedResult<TaskListItemDto>> ListAsync(Guid projectId, TaskListQuery query, CancellationToken cancellationToken)
    {
        await LoadProjectAsync(projectId, currentUser.RequireViewer(), cancellationToken);
        return await tasks.ListByProjectAsync(projectId, query, cancellationToken);
    }

    public async Task<TaskDetailsDto> GetAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var viewer = currentUser.RequireViewer();
        await LoadProjectAsync(projectId, viewer, cancellationToken);
        return await tasks.GetDetailsAsync(projectId, taskId, viewer, cancellationToken) ?? throw TaskNotFound(taskId);
    }

    public async Task<TaskDetailsDto> CreateAsync(Guid projectId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var viewer = currentUser.RequireViewer();
        var project = await LoadProjectAsync(projectId, viewer, cancellationToken);
        EnsureCanManage(viewer, project);

        if (request.AssigneeId is { } assigneeId)
        {
            await EnsureAssignableAsync(project.Id, assigneeId, cancellationToken);
        }

        var task = TaskItem.Create(
            project.Id,
            request.Title,
            request.Description,
            request.Priority ?? TaskPriority.Medium,
            request.DueDate,
            viewer.UserId);
        task.AssignTo(request.AssigneeId);

        tasks.Add(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogTaskCreated(logger, viewer.UserId, task.Id, project.Id);
        return await GetAsync(project.Id, task.Id, cancellationToken);
    }

    public async Task<TaskDetailsDto> UpdateAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var (task, viewer) = await LoadTaskForManageAsync(projectId, taskId, cancellationToken);

        // A new due date may not be in the past; an existing (possibly overdue) one may be kept as is.
        if (request.DueDate is { } dueDate
            && dueDate != task.DueDate
            && dueDate < DueDateRules.EarliestAllowed(timeProvider))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["dueDate"] = [DueDateRules.PastMessage] });
        }

        task.Update(request.Title, request.Description, request.Priority!.Value, request.DueDate);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogTaskUpdated(logger, viewer.UserId, task.Id);
        return await GetAsync(projectId, task.Id, cancellationToken);
    }

    public async Task<TaskDetailsDto> AssignAsync(Guid projectId, Guid taskId, AssignTaskRequest request, CancellationToken cancellationToken)
    {
        var (task, viewer) = await LoadTaskForManageAsync(projectId, taskId, cancellationToken);
        var assigneeId = request.AssigneeId!.Value; // non-null guaranteed by the validator

        if (task.AssigneeId != assigneeId)
        {
            await EnsureAssignableAsync(task.ProjectId, assigneeId, cancellationToken);
            task.AssignTo(assigneeId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogTaskAssigned(logger, viewer.UserId, task.Id, assigneeId);
        }

        return await GetAsync(projectId, task.Id, cancellationToken);
    }

    public async Task<TaskDetailsDto> UnassignAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var (task, viewer) = await LoadTaskForManageAsync(projectId, taskId, cancellationToken);

        if (task.AssigneeId is not null)
        {
            task.AssignTo(null);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogTaskUnassigned(logger, viewer.UserId, task.Id);
        }

        return await GetAsync(projectId, task.Id, cancellationToken);
    }

    public async Task<TaskDetailsDto> ChangeStatusAsync(
        Guid projectId,
        Guid taskId,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var viewer = currentUser.RequireViewer();
        var project = await LoadProjectAsync(projectId, viewer, cancellationToken);
        var task = await tasks.GetInProjectAsync(project.Id, taskId, cancellationToken) ?? throw TaskNotFound(taskId);
        var newStatus = request.Status!.Value; // non-null guaranteed by the validator

        if (!TaskAccess.CanManage(viewer, project.OwnerId))
        {
            // Any role may move its own task. Inactive users never get here: every access token is
            // checked against the database during authentication.
            if (!TaskAccess.IsAssignee(viewer, task.AssigneeId))
            {
                throw new ForbiddenException("You can only update the status of tasks assigned to you.");
            }

            if (task.IsReopening(newStatus))
            {
                throw new ForbiddenException("Only the project owner or an Admin can reopen a cancelled task.");
            }
        }

        var previousStatus = task.Status;
        task.ChangeStatus(newStatus, timeProvider.GetUtcNow()); // DomainException (422) for disallowed transitions
        if (previousStatus != task.Status)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogStatusChanged(logger, viewer.UserId, task.Id, previousStatus, task.Status);
        }

        return await GetAsync(projectId, task.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var (task, viewer) = await LoadTaskForManageAsync(projectId, taskId, cancellationToken);

        tasks.Remove(task); // soft delete via interceptor
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogTaskDeleted(logger, viewer.UserId, task.Id);
    }

    /// <summary>404 unless the project exists and the viewer can see it.</summary>
    private async Task<Project> LoadProjectAsync(Guid projectId, Viewer viewer, CancellationToken cancellationToken) =>
        await projects.GetVisibleAsync(projectId, viewer, cancellationToken)
        ?? throw new NotFoundException("Project", projectId);

    private async Task<(TaskItem Task, Viewer Viewer)> LoadTaskForManageAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        var viewer = currentUser.RequireViewer();
        var project = await LoadProjectAsync(projectId, viewer, cancellationToken);
        var task = await tasks.GetInProjectAsync(project.Id, taskId, cancellationToken) ?? throw TaskNotFound(taskId);
        EnsureCanManage(viewer, project);
        return (task, viewer);
    }

    private static void EnsureCanManage(Viewer viewer, Project project)
    {
        if (!TaskAccess.CanManage(viewer, project.OwnerId))
        {
            throw new ForbiddenException("Only the project owner or an Admin can create, edit, assign or delete tasks.");
        }
    }

    /// <summary>
    /// Reported as a field error on assigneeId. Unknown users get the same message as non-members,
    /// so the response does not reveal which accounts exist.
    /// </summary>
    private async Task EnsureAssignableAsync(Guid projectId, Guid assigneeId, CancellationToken cancellationToken)
    {
        var isMember = await projects.IsMemberAsync(projectId, assigneeId, cancellationToken);
        var user = isMember ? await users.GetByIdAsync(assigneeId, cancellationToken) : null;
        if (user is not { IsActive: true })
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["assigneeId"] = [AssigneeNotMember] });
        }
    }

    private static NotFoundException TaskNotFound(Guid taskId) => new("Task", taskId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} created task {TaskId} in project {ProjectId}")]
    private static partial void LogTaskCreated(ILogger logger, Guid actorId, Guid taskId, Guid projectId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} updated task {TaskId}")]
    private static partial void LogTaskUpdated(ILogger logger, Guid actorId, Guid taskId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} assigned task {TaskId} to {AssigneeId}")]
    private static partial void LogTaskAssigned(ILogger logger, Guid actorId, Guid taskId, Guid assigneeId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} unassigned task {TaskId}")]
    private static partial void LogTaskUnassigned(ILogger logger, Guid actorId, Guid taskId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} changed task {TaskId} status from {PreviousStatus} to {NewStatus}")]
    private static partial void LogStatusChanged(ILogger logger, Guid actorId, Guid taskId, TaskItemStatus previousStatus, TaskItemStatus newStatus);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {ActorId} deleted task {TaskId}")]
    private static partial void LogTaskDeleted(ILogger logger, Guid actorId, Guid taskId);
}
