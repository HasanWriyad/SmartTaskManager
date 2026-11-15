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
/// Filters shared by the task lists. Search matches title and description. Without sortBy the order
/// is priority (highest first), then due date (none last), then creation time.
/// </summary>
public abstract class TaskFilterQuery : PagedQuery
{
    public TaskItemStatus? Status { get; init; }

    public TaskPriority? Priority { get; init; }

    public Guid? AssigneeId { get; init; }

    /// <summary>Inclusive lower bound on the due date; tasks without a due date are excluded.</summary>
    public DateOnly? DueFrom { get; init; }

    /// <summary>Inclusive upper bound on the due date; tasks without a due date are excluded.</summary>
    public DateOnly? DueTo { get; init; }
}

/// <summary>Tasks of one project.</summary>
public sealed class TaskListQuery : TaskFilterQuery;

/// <summary>Tasks across every project the caller can see.</summary>
public sealed class AllTasksListQuery : TaskFilterQuery
{
    /// <summary>Limits the list to one project; a project the caller cannot see gives an empty list.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>When true, only overdue tasks: due before today (UTC) and To Do or In Progress.</summary>
    public bool Overdue { get; init; }
}

internal abstract class TaskFilterQueryValidator<T> : PagedQueryValidator<T>
    where T : TaskFilterQuery
{
    protected TaskFilterQueryValidator()
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

internal sealed class TaskListQueryValidator : TaskFilterQueryValidator<TaskListQuery>;

internal sealed class AllTasksListQueryValidator : TaskFilterQueryValidator<AllTasksListQuery>
{
    public AllTasksListQueryValidator()
    {
        RuleFor(x => x.ProjectId).NotEqual(Guid.Empty);
    }
}
