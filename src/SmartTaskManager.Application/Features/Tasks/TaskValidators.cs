using FluentValidation;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Features.Tasks;

internal static class DueDateRules
{
    public const string PastMessage = "Due date cannot be in the past.";

    /// <summary>
    /// Earliest acceptable due date: yesterday in UTC, so users west of UTC can still pick their
    /// local "today" late in their day.
    /// </summary>
    public static DateOnly EarliestAllowed(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1);
}

internal sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(TaskItem.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(TaskItem.DescriptionMaxLength);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.DueDate)
            .Must(d => d is null || d >= DueDateRules.EarliestAllowed(timeProvider))
            .WithMessage(DueDateRules.PastMessage);
        RuleFor(x => x.AssigneeId).NotEqual(Guid.Empty).When(x => x.AssigneeId is not null);
    }
}

/// <summary>The past-due-date rule for updates depends on the stored value, so it is checked in the service.</summary>
internal sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(TaskItem.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(TaskItem.DescriptionMaxLength);
        RuleFor(x => x.Priority).NotNull().IsInEnum();
    }
}

internal sealed class AssignTaskRequestValidator : AbstractValidator<AssignTaskRequest>
{
    public AssignTaskRequestValidator()
    {
        RuleFor(x => x.AssigneeId).NotNull().NotEqual(Guid.Empty);
    }
}

internal sealed class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotNull().IsInEnum();
    }
}
