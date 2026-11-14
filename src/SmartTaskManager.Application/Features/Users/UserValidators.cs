using FluentValidation;

namespace SmartTaskManager.Application.Features.Users;

internal sealed class ChangeUserRoleRequestValidator : AbstractValidator<ChangeUserRoleRequest>
{
    public ChangeUserRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}

internal sealed class ChangeUserStatusRequestValidator : AbstractValidator<ChangeUserStatusRequest>
{
    public ChangeUserStatusRequestValidator()
    {
        RuleFor(x => x.IsActive).NotNull();
    }
}
