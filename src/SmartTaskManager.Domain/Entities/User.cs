using SmartTaskManager.Domain.Common;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Domain.Entities;

public sealed class User : AuditableEntity
{
    public const int EmailMaxLength = 256;
    public const int FullNameMaxLength = 100;

    private User()
    {
    }

    /// <summary>Stored trimmed and lower-cased; unique.</summary>
    public string Email { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public static User Create(string email, string fullName, UserRole role) => new()
    {
        Email = NormalizeEmail(email),
        FullName = fullName.Trim(),
        Role = role,
        IsActive = true,
    };

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void UpdateProfile(string fullName) => FullName = fullName.Trim();

    public void ChangeRole(UserRole role) => Role = role;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
