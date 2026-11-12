using FluentValidation;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Features.Auth;

internal static class PasswordRules
{
    public const int MinLength = 8;

    /// <summary>Upper bound keeps PBKDF2 hashing cost bounded for hostile input.</summary>
    public const int MaxLength = 128;
}

internal sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(User.EmailMaxLength)
            .EmailAddress();

        RuleFor(x => x.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(User.FullNameMaxLength)
            .Must(name => name.Trim().Length >= 2).WithMessage("Full name must be at least 2 characters.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(PasswordRules.MinLength)
            .MaximumLength(PasswordRules.MaxLength)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a symbol.");
    }
}

/// <summary>Only presence and size are checked at login; complexity rules apply when a password is set.</summary>
internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}
