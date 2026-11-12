using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Infrastructure.Persistence;

namespace SmartTaskManager.Infrastructure.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    // No tracking: revocation goes through set-based updates, never through this entity.
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .AsNoTracking()
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public async Task<bool> TryRevokeAsync(
        Guid tokenId,
        DateTimeOffset revokedAt,
        Guid? replacedByTokenId,
        CancellationToken cancellationToken)
    {
        var affected = await db.RefreshTokens
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.RevokedAt, revokedAt)
                    .SetProperty(t => t.ReplacedByTokenId, replacedByTokenId),
                cancellationToken);

        return affected == 1;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, revokedAt), cancellationToken);

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, revokedAt), cancellationToken);

    public Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
}
