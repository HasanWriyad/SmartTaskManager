using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTaskManager.Api.Authorization;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Application.Features.Projects;

namespace SmartTaskManager.Api.Controllers;

/// <summary>
/// Projects. Everyone sees only projects they own or are a member of (Admins see all); projects
/// outside that set return 404. Changes require an Admin or the owning ProjectManager.
/// </summary>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public sealed class ProjectsController(IProjectService projectService) : ControllerBase
{
    /// <summary>
    /// Lists the projects the caller can see. Search matches name and description;
    /// sortBy: name (default), createdAt, taskCount.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<ProjectListItemDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] ProjectListQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await projectService.ListAsync(query, cancellationToken)));

    /// <summary>Project details, including members.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<ProjectDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await projectService.GetAsync(id, cancellationToken)));

    /// <summary>Creates a project owned by the caller.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<ProjectDetailsDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, ApiResponse.Ok(project, "Project created."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<ProjectDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await projectService.UpdateAsync(id, request, cancellationToken), "Project updated."));

    /// <summary>Soft-deletes the project and its tasks.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await projectService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok("Project deleted."));
    }

    /// <summary>Adds an active user to the project.</summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<ProjectDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMember(Guid id, AddProjectMemberRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await projectService.AddMemberAsync(id, request, cancellationToken), "Member added."));

    /// <summary>Removes a member; their tasks in this project become unassigned.</summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<ProjectDetailsDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await projectService.RemoveMemberAsync(id, userId, cancellationToken), "Member removed."));
}
