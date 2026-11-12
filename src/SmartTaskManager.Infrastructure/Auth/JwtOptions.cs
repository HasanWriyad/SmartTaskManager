using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartTaskManager.Infrastructure.Auth;

/// <summary>
/// JWT settings. Issuer and Audience live in appsettings.json; SigningKey must come from
/// user-secrets or the <c>Jwt__SigningKey</c> environment variable.
/// </summary>
public sealed class JwtOptions : IValidatableObject
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 requires a key of at least 256 bits.</summary>
    public const int MinSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    [Required]
    public string SigningKey { get; init; } = null!;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 30)]
    public int RefreshTokenDays { get; init; } = 7;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(SigningKey) && Encoding.UTF8.GetByteCount(SigningKey) < MinSigningKeyBytes)
        {
            yield return new ValidationResult(
                $"Jwt:SigningKey must be at least {MinSigningKeyBytes} bytes.",
                [nameof(SigningKey)]);
        }
    }
}
