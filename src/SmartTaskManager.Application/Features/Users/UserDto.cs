using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Users;

public sealed record UserDto(Guid Id, string Email, string FullName, UserRole Role);

public static class UserMappings
{
    public static UserDto ToDto(this User user) => new(user.Id, user.Email, user.FullName, user.Role);
}
