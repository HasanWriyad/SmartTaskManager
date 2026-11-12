using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTaskManager.Api.Authorization;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Tasks;

namespace SmartTaskManager.Api.Controllers;

/// <summary>
/// Tasks of a project. The project must be visible to the caller (404 otherwise). Creating, editing,
/// assigning and deleting require an Admin or the owning ProjectManager; any active assignee may
/// change the status of their own task.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Produces("application/json")]
public sealed class TasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>Lists the project's tasks: highest priority first, then earliest due date.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TaskListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid projectId, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await taskService.ListAsync(projectId, cancellationToken)));

    [HttpGet("{taskId:guid}")]
    [ProducesResponseType<ApiResponse<TaskDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await taskService.GetAsync(projectId, taskId, cancellationToken)));

    /// <summary>Creates a task (status To Do). Priority defaults to Medium; the assignee must be an active project member.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<TaskDetailsDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid projectId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await taskService.CreateAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId, taskId = task.Id }, ApiResponse.Ok(task, "Task created."));
    }

    /// <summary>Updates title, description, priority and due date (null clears the due date).</summary>
    [HttpPut("{taskId:guid}")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<TaskDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid projectId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await taskService.UpdateAsync(projectId, taskId, request, cancellationToken), "Task updated."));

    /// <summary>Assigns or reassigns the task to an active project member.</summary>
    [HttpPut("{taskId:guid}/assignee")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<TaskDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(Guid projectId, Guid taskId, AssignTaskRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await taskService.AssignAsync(projectId, taskId, request, cancellationToken), "Task assigned."));

    [HttpDelete("{taskId:guid}/assignee")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<TaskDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unassign(Guid projectId, Guid taskId, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await taskService.UnassignAsync(projectId, taskId, cancellationToken), "Task unassigned."));

    /// <summary>
    /// Changes the status. A cancelled task can only be reopened to ToDo, and only by the owner or an
    /// Admin. Completing sets completedAt; any other status clears it.
    /// </summary>
    [HttpPut("{taskId:guid}/status")]
    [ProducesResponseType<ApiResponse<TaskDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangeStatus(Guid projectId, Guid taskId, UpdateTaskStatusRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await taskService.ChangeStatusAsync(projectId, taskId, request, cancellationToken), "Status updated."));

    /// <summary>Soft-deletes the task.</summary>
    [HttpDelete("{taskId:guid}")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        await taskService.DeleteAsync(projectId, taskId, cancellationToken);
        return Ok(ApiResponse.Ok("Task deleted."));
    }
}
