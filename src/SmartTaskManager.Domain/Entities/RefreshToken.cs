using SmartTaskManager.Domain.Common;

namespace SmartTaskManager.Domain.Entities;

/// <summary>
/// A refresh token issued to a user. Only the SHA-256 hash of the token value is stored.
/// Tokens are rotated: each refresh revokes the current token and records its replacement.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public const int TokenHashLength = 64; // SHA-256 as hex

    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt) => new()
    {
        UserId = userId,
        TokenHash = tokenHash,
        CreatedAt = createdAt,
        ExpiresAt = expiresAt,
    };

    public bool IsRevoked => RevokedAt is not null;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && now < ExpiresAt;

    public void Revoke(DateTimeOffset at, Guid? replacedByTokenId = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = at;
        ReplacedByTokenId = replacedByTokenId;
    }
}
