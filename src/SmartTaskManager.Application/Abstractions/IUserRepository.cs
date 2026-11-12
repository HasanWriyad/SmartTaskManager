using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Application.Features.Users;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Tracked load, for updates.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <param name="normalizedEmail">Email already normalized with <see cref="User.NormalizeEmail"/>.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Counts active Admins other than <paramref name="excludingUserId"/>.</summary>
    Task<int> CountOtherActiveAdminsAsync(Guid excludingUserId, CancellationToken cancellationToken);

    Task<PagedResult<UserAdminDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken);

    Task<PagedResult<UserLookupDto>> LookupActiveAsync(UserLookupQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Takes an exclusive lock, held until the current transaction ends, that serializes changes
    /// to roles and active status. Must be called inside <see cref="IUnitOfWork.BeginTransactionAsync"/>.
    /// </summary>
    Task AcquireRoleChangeLockAsync(CancellationToken cancellationToken);

    void Add(User user);
}
