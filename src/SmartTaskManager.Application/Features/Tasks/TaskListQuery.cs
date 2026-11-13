using FluentValidation;
using SmartTaskManager.Application.Common.Paging;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Tasks;

public static class TaskSortFields
{
    public const string Title = "title";
    public const string Priority = "priority";
    public const string Status = "status";
    public const string DueDate = "dueDate";
    public const string CreatedAt = "createdAt";

    public static readonly IReadOnlyCollection<string> All = [Title, Priority, Status, DueDate, CreatedAt];
}

/// <summary>
/// Tasks of one project. Search matches title and description. Without sortBy the order is
/// priority (highest first), then due date (none last), then creation time.
/// </summary>
public sealed class TaskListQuery : PagedQuery
{
    public TaskItemStatus? Status { get; init; }

    public TaskPriority? Priority { get; init; }

    public Guid? AssigneeId { get; init; }

    /// <summary>Inclusive lower bound on the due date; tasks without a due date are excluded.</summary>
    public DateOnly? DueFrom { get; init; }

    /// <summary>Inclusive upper bound on the due date; tasks without a due date are excluded.</summary>
    public DateOnly? DueTo { get; init; }
}

internal sealed class TaskListQueryValidator : PagedQueryValidator<TaskListQuery>
{
    public TaskListQueryValidator()
        : base(TaskSortFields.All)
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.AssigneeId).NotEqual(Guid.Empty);
        RuleFor(x => x.DueTo)
            .GreaterThanOrEqualTo(x => x.DueFrom)
            .When(x => x.DueFrom is not null && x.DueTo is not null)
            .WithMessage("DueTo must be on or after DueFrom.");
    }
}
