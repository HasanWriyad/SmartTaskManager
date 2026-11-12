using SmartTaskManager.Application.Common.Paging;

namespace SmartTaskManager.Application.Features.Users;

public interface IUserService
{
    /// <summary>Admin: all users with filters, sorting and paging.</summary>
    Task<PagedResult<UserAdminDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken);

    /// <summary>Admin and ProjectManager: active users for member/assignee pickers.</summary>
    Task<PagedResult<UserLookupDto>> LookupAsync(UserLookupQuery query, CancellationToken cancellationToken);

    /// <summary>Admin: changes another user's role. The last active Admin cannot be demoted.</summary>
    Task<UserAdminDto> ChangeRoleAsync(Guid userId, ChangeUserRoleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Admin: activates or deactivates another user. Deactivation ends the user's sessions.
    /// The last active Admin cannot be deactivated.
    /// </summary>
    Task<UserAdminDto> ChangeStatusAsync(Guid userId, ChangeUserStatusRequest request, CancellationToken cancellationToken);
}
