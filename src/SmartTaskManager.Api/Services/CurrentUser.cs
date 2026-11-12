using System.Security.Claims;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Enums;
using SmartTaskManager.Infrastructure.Auth;

namespace SmartTaskManager.Api.Services;

/// <summary>Reads the caller's identity from the JWT claims of the current HTTP request.</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirst(AppClaimTypes.UserId)?.Value, out var id)
            ? id
            : null;

    public UserRole? Role =>
        IsAuthenticated
        && Enum.TryParse<UserRole>(Principal!.FindFirst(AppClaimTypes.Role)?.Value, ignoreCase: false, out var role)
        && Enum.IsDefined(role)
            ? role
            : null;
}
