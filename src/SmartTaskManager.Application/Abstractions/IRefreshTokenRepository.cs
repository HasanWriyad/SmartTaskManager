using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

public interface IRefreshTokenRepository
{
    /// <summary>Finds a token by its hash, including its <see cref="RefreshToken.User"/>.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Tracks a new token; it is inserted by the next <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(RefreshToken token);

    /// <summary>
    /// Atomically revokes a token only if it is not revoked yet (executes immediately).
    /// Returns false when another request already revoked it, i.e. the token was reused.
    /// </summary>
    Task<bool> TryRevokeAsync(Guid tokenId, DateTimeOffset revokedAt, Guid? replacedByTokenId, CancellationToken cancellationToken);

    /// <summary>Revokes every still-active token in the family (executes immediately).</summary>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the user's expired tokens (executes immediately). Revoked but unexpired tokens are kept,
    /// because they are what makes reuse detectable.
    /// </summary>
    Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}
