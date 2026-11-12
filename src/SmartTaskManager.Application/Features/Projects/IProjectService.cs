namespace SmartTaskManager.Application.Features.Projects;

/// <summary>
/// Project operations, enforcing the role-permission matrix for the current user. Projects the
/// caller cannot see are reported as not found (404); visible ones they cannot change as forbidden (403).
/// </summary>
public interface IProjectService
{
    /// <summary>Projects the caller can see, ordered by name.</summary>
    Task<IReadOnlyList<ProjectListItemDto>> ListAsync(CancellationToken cancellationToken);

    Task<ProjectDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Admin or ProjectManager; the caller becomes the owner and its first member.</summary>
    Task<ProjectDetailsDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectDetailsDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the project and its tasks in one transaction.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds an active user as a member.</summary>
    Task<ProjectDetailsDto> AddMemberAsync(Guid id, AddProjectMemberRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a member and unassigns their tasks in this project, in one transaction.
    /// The owner cannot be removed (409).
    /// </summary>
    Task<ProjectDetailsDto> RemoveMemberAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}
