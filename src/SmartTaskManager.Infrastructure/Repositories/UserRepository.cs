using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Application.Features.Users;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;
using SmartTaskManager.Infrastructure.Persistence;

namespace SmartTaskManager.Infrastructure.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    /// <summary>
    /// sp_getapplock in transaction scope: released automatically on commit or rollback.
    /// A negative return code means the lock wasn't granted (timeout/deadlock), so fail loudly.
    /// </summary>
    private const string AcquireRoleChangeLockSql = """
        DECLARE @result int;
        EXEC @result = sp_getapplock
            @Resource = N'SmartTaskManager:user-role-changes',
            @LockMode = N'Exclusive',
            @LockOwner = N'Transaction',
            @LockTimeout = 10000;
        IF @result < 0 THROW 51000, N'Could not acquire the user role-change lock.', 1;
        """;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<int> CountOtherActiveAdminsAsync(Guid excludingUserId, CancellationToken cancellationToken) =>
        db.Users.CountAsync(
            u => u.Role == UserRole.Admin && u.IsActive && u.Id != excludingUserId,
            cancellationToken);

    public Task<PagedResult<UserAdminDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users = Search(db.Users.AsNoTracking(), query.Search);

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        var ordered = query switch
        {
            _ when query.IsSortField(UserSortFields.Email) => users.OrderByDirection(u => u.Email, query.SortDirection),
            _ when query.IsSortField(UserSortFields.Role) => users.OrderByDirection(u => u.Role, query.SortDirection),
            _ when query.IsSortField(UserSortFields.CreatedAt) => users.OrderByDirection(u => u.CreatedAt, query.SortDirection),
            _ => users.OrderByDirection(u => u.FullName, query.SortDirection),
        };

        return ordered
            .ThenBy(u => u.Id)
            .Select(UserMappings.AdminProjection)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public Task<PagedResult<UserLookupDto>> LookupActiveAsync(UserLookupQuery query, CancellationToken cancellationToken)
    {
        var users = Search(db.Users.AsNoTracking().Where(u => u.IsActive), query.Search);

        var ordered = query.IsSortField(UserSortFields.Email)
            ? users.OrderByDirection(u => u.Email, query.SortDirection)
            : users.OrderByDirection(u => u.FullName, query.SortDirection);

        return ordered
            .ThenBy(u => u.Id)
            .Select(UserMappings.LookupProjection)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task AcquireRoleChangeLockAsync(CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The role-change lock must be acquired inside a transaction.");
        }

        await db.Database.ExecuteSqlRawAsync(AcquireRoleChangeLockSql, cancellationToken);
    }

    public void Add(User user) => db.Users.Add(user);

    /// <summary>Case-insensitive (database collation) substring match on name or email.</summary>
    private static IQueryable<User> Search(IQueryable<User> users, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return users;
        }

        var term = search.Trim();
        return users.Where(u => u.FullName.Contains(term) || u.Email.Contains(term));
    }
}
