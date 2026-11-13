using FluentValidation;
using SmartTaskManager.Application.Common.Paging;

namespace SmartTaskManager.Application.Features.Projects;

public static class ProjectSortFields
{
    public const string Name = "name";
    public const string CreatedAt = "createdAt";
    public const string TaskCount = "taskCount";

    public static readonly IReadOnlyCollection<string> All = [Name, CreatedAt, TaskCount];
}

/// <summary>
/// Projects visible to the caller. Search matches name and description.
/// Default order: name ascending.
/// </summary>
public sealed class ProjectListQuery : PagedQuery
{
    /// <summary>Only projects owned by this user.</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>true: only projects the caller owns; false: only projects owned by others; omitted: no filter.</summary>
    public bool? OwnedByMe { get; init; }

    /// <summary>Inclusive lower bound on the creation date (UTC).</summary>
    public DateOnly? CreatedFrom { get; init; }

    /// <summary>Inclusive upper bound on the creation date (UTC).</summary>
    public DateOnly? CreatedTo { get; init; }
}

internal sealed class ProjectListQueryValidator : PagedQueryValidator<ProjectListQuery>
{
    public ProjectListQueryValidator()
        : base(ProjectSortFields.All)
    {
        RuleFor(x => x.OwnerId).NotEqual(Guid.Empty);
        RuleFor(x => x.OwnedByMe)
            .Null()
            .When(x => x.OwnerId is not null)
            .WithMessage("Use either OwnerId or OwnedByMe, not both.");
        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom)
            .When(x => x.CreatedFrom is not null && x.CreatedTo is not null)
            .WithMessage("CreatedTo must be on or after CreatedFrom.");
    }
}
