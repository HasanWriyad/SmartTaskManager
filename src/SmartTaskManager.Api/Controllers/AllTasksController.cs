using Microsoft.AspNetCore.Mvc;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Application.Features.Tasks;

namespace SmartTaskManager.Api.Controllers;

/// <summary>Tasks across all projects the caller can see. Changes go through the project-scoped task routes.</summary>
[ApiController]
[Route("api/tasks")]
[Tags("Tasks")]
[Produces("application/json")]
public sealed class AllTasksController(ITaskService taskService) : ControllerBase
{
    /// <summary>
    /// Lists tasks across all projects the caller can see. Those are the projects they own or are a
    /// member of (Admin: all projects); each row includes the project name. Same search, filters and
    /// sorting as the project task list, plus projectId (a project the caller cannot see gives an
    /// empty list) and overdue=true (due before today, UTC, and ToDo or InProgress).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<TaskListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] AllTasksListQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await taskService.ListAllAsync(query, cancellationToken)));
}
