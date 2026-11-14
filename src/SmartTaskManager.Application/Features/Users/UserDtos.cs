using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Users;

// None of these DTOs carries the password hash; the projections in UserMappings never read it.

public sealed record ChangeUserRoleRequest(UserRole Role);

/// <summary>Nullable so an omitted field is a validation error instead of silently meaning "deactivate".</summary>
public sealed record ChangeUserStatusRequest(bool? IsActive);

/// <summary>The signed-in user, returned by auth endpoints.</summary>
public sealed record UserDto(Guid Id, string Email, string FullName, UserRole Role);

/// <summary>Full user view for Admin user management.</summary>
public sealed record UserAdminDto(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Minimal view for picking a user when adding project members or assigning tasks.</summary>
public sealed record UserLookupDto(Guid Id, string FullName, string Email);
