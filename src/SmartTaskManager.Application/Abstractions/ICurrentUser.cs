using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Abstractions;

/// <summary>The authenticated caller of the current request, if any.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    UserRole? Role { get; }

    bool IsAuthenticated { get; }
}
