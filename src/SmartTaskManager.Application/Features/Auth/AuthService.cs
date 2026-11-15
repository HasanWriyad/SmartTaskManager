using Microsoft.Extensions.Logging;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common.Exceptions;
using SmartTaskManager.Application.Features.Users;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Auth;

internal sealed partial class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string SessionExpired = "Your session has expired. Please sign in again.";
    private const string AccountDeactivated = "This account has been deactivated.";

    /// <summary>Hashed against when the email is unknown, so response time doesn't reveal which emails exist.</summary>
    private static readonly User TimingDummyUser = User.Create("timing@invalid.local", "Timing", UserRole.TeamMember);

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        // Role is fixed here, never taken from the request; only an Admin can promote users.
        var user = User.Create(email, request.FullName, UserRole.TeamMember);
        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogRegistered(logger, user.Id);
        return user.ToDto();
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        if (user is null)
        {
            passwordHasher.HashPassword(TimingDummyUser, request.Password);
            LogLoginFailed(logger);
            throw new UnauthorizedException(InvalidCredentials);
        }

        var verification = passwordHasher.VerifyPassword(user, request.Password);
        if (verification == PasswordVerification.Failed)
        {
            LogLoginFailed(logger);
            throw new UnauthorizedException(InvalidCredentials);
        }

        // Checked after the password, so the message only goes to someone who knows the credentials.
        if (!user.IsActive)
        {
            LogInactiveUserBlocked(logger, user.Id);
            throw new UnauthorizedException(AccountDeactivated);
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));
        }

        var now = timeProvider.GetUtcNow();
        await refreshTokens.DeleteExpiredAsync(user.Id, now, cancellationToken);

        var result = IssueTokens(user, familyId: Guid.NewGuid(), now, out _);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        LogLoggedIn(logger, user.Id);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException(SessionExpired);
        }

        var now = timeProvider.GetUtcNow();
        var current = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(refreshToken), cancellationToken);
        if (current is null || current.IsExpired(now))
        {
            throw new UnauthorizedException(SessionExpired);
        }

        if (current.IsRevoked)
        {
            // Rotated tokens have a replacement: presenting one again means it was copied (reuse).
            // Tokens revoked by logout/deactivation have none, and their family is already revoked.
            throw current.ReplacedByTokenId is not null
                ? await RevokeFamilyOnReuseAsync(current, now, cancellationToken)
                : new UnauthorizedException(SessionExpired);
        }

        if (!current.User.IsActive)
        {
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            LogInactiveUserBlocked(logger, current.UserId);
            throw new UnauthorizedException(AccountDeactivated);
        }

        var result = IssueTokens(current.User, current.FamilyId, now, out var replacementId);

        // Claim the old token atomically. If a concurrent request already rotated it, this
        // presentation is a reuse. Runs before the insert below: if the insert fails, the session
        // ends (fail closed) rather than leaving two valid tokens.
        if (!await refreshTokens.TryRevokeAsync(current.Id, now, replacementId, cancellationToken))
        {
            throw await RevokeFamilyOnReuseAsync(current, now, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var token = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(refreshToken), cancellationToken);
        if (token is null)
        {
            return;
        }

        await refreshTokens.RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        LogLoggedOut(logger, token.UserId);
    }

    private AuthResult IssueTokens(User user, Guid familyId, DateTimeOffset now, out Guid refreshTokenId)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        var generated = tokenService.GenerateRefreshToken();

        var entity = RefreshToken.Create(user.Id, familyId, generated.Hash, now, generated.ExpiresAt);
        refreshTokens.Add(entity); // the key is generated on Add, so the id is known before saving
        refreshTokenId = entity.Id;

        var response = new AuthResponse(accessToken.Token, accessToken.ExpiresAt, user.ToDto());
        return new AuthResult(response, generated.Token, generated.ExpiresAt);
    }

    /// <summary>Revokes the whole family and returns the exception for the caller to throw.</summary>
    private async Task<UnauthorizedException> RevokeFamilyOnReuseAsync(
        RefreshToken token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await refreshTokens.RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
        LogTokenReuse(logger, token.UserId, token.FamilyId);
        return new UnauthorizedException(SessionExpired);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} registered")]
    private static partial void LogRegistered(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} logged in")]
    private static partial void LogLoggedIn(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} logged out")]
    private static partial void LogLoggedOut(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed: invalid credentials")]
    private static partial void LogLoginFailed(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blocked authentication for inactive user {UserId}")]
    private static partial void LogInactiveUserBlocked(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserId}; revoked token family {FamilyId}")]
    private static partial void LogTokenReuse(ILogger logger, Guid userId, Guid familyId);
}
