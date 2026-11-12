using System.Linq.Expressions;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Users;

// None of these DTOs carries the password hash; projections below never read it.

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

public static class UserMappings
{
    /// <summary>SQL projection: selects only the listed columns.</summary>
    public static readonly Expression<Func<User, UserAdminDto>> AdminProjection =
        u => new UserAdminDto(u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt, u.UpdatedAt);

    public static readonly Expression<Func<User, UserLookupDto>> LookupProjection =
        u => new UserLookupDto(u.Id, u.FullName, u.Email);

    private static readonly Func<User, UserAdminDto> ToAdminDtoCompiled = AdminProjection.Compile();

    public static UserDto ToDto(this User user) => new(user.Id, user.Email, user.FullName, user.Role);

    public static UserAdminDto ToAdminDto(this User user) => ToAdminDtoCompiled(user);
}
