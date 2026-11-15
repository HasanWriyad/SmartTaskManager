using SmartTaskManager.Domain.Common;

namespace SmartTaskManager.Domain.Entities;

/// <summary>
/// A refresh token issued to a user. Only the SHA-256 hash of the token value is stored.
/// Tokens are rotated: each refresh revokes the current token and issues a replacement in the same
/// <see cref="FamilyId"/>. A family is one login session, so reuse of a revoked token revokes the family.
/// </summary>
public sealed class RefreshToken : BaseEntity
{
    public const int TokenHashLength = 64; // SHA-256 as hex

    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    /// <summary>Shared by every token produced by rotation from one login.</summary>
    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Create(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) => new()
    {
        UserId = userId,
        FamilyId = familyId,
        TokenHash = tokenHash,
        CreatedAt = createdAt,
        ExpiresAt = expiresAt,
    };

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
