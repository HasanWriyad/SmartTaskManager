using System.Linq.Expressions;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Features.Users;

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
