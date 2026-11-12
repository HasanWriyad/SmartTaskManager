using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common.Exceptions;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Common;

/// <summary>The authenticated caller, as seen by permission checks.</summary>
public sealed record Viewer(Guid UserId, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;

    public bool IsProjectManager => Role == UserRole.ProjectManager;
}

public static class CurrentUserExtensions
{
    /// <summary>Returns the caller, or throws 401 if the request is not authenticated with a known role.</summary>
    public static Viewer RequireViewer(this ICurrentUser currentUser) =>
        currentUser is { UserId: { } id, Role: { } role }
            ? new Viewer(id, role)
            : throw new UnauthorizedException("Authentication is required.");
}
