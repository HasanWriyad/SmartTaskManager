using Microsoft.Extensions.Logging;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common.Exceptions;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Users;

internal sealed partial class UserService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<UserService> logger) : IUserService
{
    public Task<PagedResult<UserAdminDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken) =>
        users.ListAsync(query, cancellationToken);

    public Task<PagedResult<UserLookupDto>> LookupAsync(UserLookupQuery query, CancellationToken cancellationToken) =>
        users.LookupActiveAsync(query, cancellationToken);

    public async Task<UserAdminDto> ChangeRoleAsync(Guid userId, ChangeUserRoleRequest request, CancellationToken cancellationToken)
    {
        var actorId = RequireActorId();
        if (userId == actorId)
        {
            throw new ForbiddenException("You cannot change your own role.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await LoadForRoleChangeAsync(userId, cancellationToken);
        if (user.Role == request.Role)
        {
            return user.ToAdminDto();
        }

        if (IsActiveAdmin(user))
        {
            await EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
        }

        var previousRole = user.Role;
        user.ChangeRole(request.Role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // The user's current access token keeps the old role until it expires (max 15 min);
        // the next refresh issues a token with the new role.
        LogRoleChanged(logger, actorId, user.Id, previousRole, user.Role);
        return user.ToAdminDto();
    }

    public async Task<UserAdminDto> ChangeStatusAsync(Guid userId, ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        var actorId = RequireActorId();
        var activate = request.IsActive!.Value; // non-null guaranteed by the validator
        if (userId == actorId && !activate)
        {
            throw new ForbiddenException("You cannot deactivate your own account.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await LoadForRoleChangeAsync(userId, cancellationToken);
        if (user.IsActive == activate)
        {
            return user.ToAdminDto();
        }

        if (activate)
        {
            user.Activate();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (IsActiveAdmin(user))
            {
                await EnsureAnotherActiveAdminAsync(user.Id, cancellationToken);
            }

            user.Deactivate();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            // End existing sessions now rather than at their next refresh attempt.
            await refreshTokens.RevokeAllForUserAsync(user.Id, timeProvider.GetUtcNow(), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        LogStatusChanged(logger, actorId, user.Id, user.IsActive);
        return user.ToAdminDto();
    }

    /// <summary>Serializes role/status changes, then loads the target, so the Admin count can't change underneath us.</summary>
    private async Task<User> LoadForRoleChangeAsync(Guid userId, CancellationToken cancellationToken)
    {
        await users.AcquireRoleChangeLockAsync(cancellationToken);
        return await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User", userId);
    }

    private async Task EnsureAnotherActiveAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await users.CountOtherActiveAdminsAsync(userId, cancellationToken) == 0)
        {
            throw new ConflictException("The last active Admin cannot be demoted or deactivated.");
        }
    }

    private static bool IsActiveAdmin(User user) => user.IsActive && user.Role == UserRole.Admin;

    private Guid RequireActorId() => currentUser.UserId ?? throw new UnauthorizedException("Authentication is required.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} changed role of user {UserId} from {PreviousRole} to {NewRole}")]
    private static partial void LogRoleChanged(ILogger logger, Guid actorId, Guid userId, UserRole previousRole, UserRole newRole);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} set user {UserId} active={IsActive}")]
    private static partial void LogStatusChanged(ILogger logger, Guid actorId, Guid userId, bool isActive);
}
