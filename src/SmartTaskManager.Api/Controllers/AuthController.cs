using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartTaskManager.Api.Auth;
using SmartTaskManager.Api.RateLimiting;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Auth;
using SmartTaskManager.Application.Features.Users;

namespace SmartTaskManager.Api.Controllers;

/// <summary>
/// Registration and session management. The access token is returned in the body; the refresh token
/// is only ever sent as an httpOnly cookie.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Creates a new account with the TeamMember role. Rate limited per IP.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ApiResponse<UserDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(user, "Registration successful."));
    }

    /// <summary>Signs in and starts a session. Sets the refresh token cookie. Rate limited per IP.</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        RefreshTokenCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(ApiResponse.Ok(result.Response));
    }

    /// <summary>
    /// Exchanges the refresh token cookie for a new access token and a rotated refresh token.
    /// Presenting an already-rotated token revokes the whole session.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType<ApiResponse<AuthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Write(Response, result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(ApiResponse.Ok(result.Response));
    }

    /// <summary>Ends the session: revokes the refresh token family and clears the cookie. Always succeeds.</summary>
    [HttpPost("logout")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return Ok(ApiResponse.Ok("Logged out."));
    }
}
