using Microsoft.AspNetCore.Identity;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Infrastructure.Auth;

/// <summary>Adapter over ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> (PBKDF2, V3 format).</summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string HashPassword(User user, string password) => _inner.HashPassword(user, password);

    public PasswordVerification VerifyPassword(User user, string providedPassword) =>
        _inner.VerifyHashedPassword(user, user.PasswordHash, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };
}
