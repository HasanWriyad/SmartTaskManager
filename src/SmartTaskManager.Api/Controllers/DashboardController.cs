using Microsoft.AspNetCore.Mvc;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Dashboard;

namespace SmartTaskManager.Api.Controllers;

/// <summary>Statistics for the projects the caller can see (all projects for an Admin).</summary>
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>
    /// Totals, tasks by status and priority, completed vs pending (pending = ToDo + InProgress;
    /// cancelled counts as neither), overdue count, and pending tasks due in the next 7 days (UTC).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<DashboardDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await dashboardService.GetAsync(cancellationToken)));
}
