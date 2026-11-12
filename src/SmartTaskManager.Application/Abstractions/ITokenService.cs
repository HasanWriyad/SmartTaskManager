using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>Generates a new random refresh token value, its hash for storage, and its expiry.</summary>
    GeneratedRefreshToken GenerateRefreshToken();

    /// <summary>Hashes a refresh token value received from a client, for lookup.</summary>
    string HashRefreshToken(string token);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <param name="Token">Plain value sent to the client once; never stored or logged.</param>
/// <param name="Hash">SHA-256 hex of <paramref name="Token"/>; what the database stores.</param>
/// <param name="ExpiresAt">When the token stops being accepted.</param>
public sealed record GeneratedRefreshToken(string Token, string Hash, DateTimeOffset ExpiresAt);
