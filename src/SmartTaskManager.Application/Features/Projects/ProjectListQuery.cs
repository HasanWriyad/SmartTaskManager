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
public sealed class ProjectListQuery : PagedQuery;

internal sealed class ProjectListQueryValidator : PagedQueryValidator<ProjectListQuery>
{
    public ProjectListQueryValidator()
        : base(ProjectSortFields.All)
    {
    }
}
