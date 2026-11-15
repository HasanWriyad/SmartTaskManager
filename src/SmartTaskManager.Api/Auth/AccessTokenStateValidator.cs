using Microsoft.AspNetCore.Authentication.JwtBearer;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Infrastructure.Auth;

namespace SmartTaskManager.Api.Auth;

/// <summary>
/// Runs after a JWT's signature and lifetime are validated. Rejects the token (401) when the user no
/// longer exists, has been deactivated, or now has a different role than the token carries, so role
/// changes and deactivation take effect on the next request rather than when the token expires.
/// The client then calls /api/auth/refresh, which issues a token with the current role (or fails for
/// an inactive user). Costs one primary-key lookup per authenticated request.
/// </summary>
internal static partial class AccessTokenStateValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var userIdClaim = principal?.FindFirst(AppClaimTypes.UserId)?.Value;
        var roleClaim = principal?.FindFirst(AppClaimTypes.Role)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || string.IsNullOrEmpty(roleClaim))
        {
            context.Fail("The token is missing the user id or role.");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var state = await users.GetAuthStateAsync(userId, context.HttpContext.RequestAborted);

        var reason = state switch
        {
            null => "user no longer exists",
            { IsActive: false } => "user is deactivated",
            _ when !string.Equals(state.Role.ToString(), roleClaim, StringComparison.Ordinal) => "role changed",
            _ => null,
        };

        if (reason is not null)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("SmartTaskManager.Api.Auth.AccessTokenStateValidator");
            LogRejected(logger, userId, reason);
            context.Fail($"Access token rejected: {reason}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rejected access token for user {UserId}: {Reason}")]
    private static partial void LogRejected(ILogger logger, Guid userId, string reason);
}
