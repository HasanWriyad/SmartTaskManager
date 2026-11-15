using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTaskManager.Api.Authorization;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Application.Features.Users;

namespace SmartTaskManager.Api.Controllers;

/// <summary>User management (Admin) and user lookup for pickers (Admin, ProjectManager).</summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Lists users. Search matches name or email; sortBy: name, email, role, createdAt.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<ApiResponse<PagedResult<UserAdminDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] UserListQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await userService.ListAsync(query, cancellationToken)));

    /// <summary>
    /// Active users for adding project members or assigning tasks. Search matches name or email;
    /// sortBy: name, email; pageSize up to 50.
    /// </summary>
    [HttpGet("lookup")]
    [Authorize(Policy = Policies.AdminOrProjectManager)]
    [ProducesResponseType<ApiResponse<PagedResult<UserLookupDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Lookup([FromQuery] UserLookupQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await userService.LookupAsync(query, cancellationToken)));

    /// <summary>Changes another user's role. You cannot change your own role or demote the last active Admin.</summary>
    [HttpPut("{id:guid}/role")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<ApiResponse<UserAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await userService.ChangeRoleAsync(id, request, cancellationToken), "Role updated."));

    /// <summary>
    /// Activates or deactivates another user. Deactivation ends their sessions. You cannot deactivate
    /// yourself or the last active Admin.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType<ApiResponse<UserAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeUserStatusRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await userService.ChangeStatusAsync(id, request, cancellationToken), "Status updated."));
}
