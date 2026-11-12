using System.Security.Claims;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Api.Services;

/// <summary>Reads the caller's identity from the JWT claims of the current HTTP request.</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Principal?.FindFirst("sub")?.Value, out var id)
            ? id
            : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirst(ClaimTypes.Role)?.Value, ignoreCase: false, out var role)
        && Enum.IsDefined(role)
            ? role
            : null;
}
