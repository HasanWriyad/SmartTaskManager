using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTaskManager.Api.Authorization;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Ai;

namespace SmartTaskManager.Api.Controllers;

/// <summary>AI assistance. Suggestions only; nothing is saved.</summary>
[ApiController]
[Route("api/ai")]
[Authorize(Policy = Policies.AdminOrProjectManager)]
[Produces("application/json")]
public sealed class AiController(IAiAssistantService aiAssistantService) : ControllerBase
{
    /// <summary>
    /// Suggests an improved task description: corrected grammar, clearer and more professional wording,
    /// expanded short text, and actionable steps. Input up to 2,000 characters. The result is not saved;
    /// apply it with a normal task update. "source" is "fallback" when no AI provider is configured.
    /// </summary>
    [HttpPost("improve-description")]
    [ProducesResponseType<ApiResponse<ImproveDescriptionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> ImproveDescription(ImproveDescriptionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await aiAssistantService.ImproveDescriptionAsync(request, cancellationToken)));
}
