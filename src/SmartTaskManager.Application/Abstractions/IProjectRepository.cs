using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Projects;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

/// <summary>Soft-deleted projects are invisible to every method here.</summary>
public interface IProjectRepository
{
    /// <summary>Tracked load, for updates. Does not apply visibility rules.</summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectListItemDto>> ListVisibleAsync(Viewer viewer, CancellationToken cancellationToken);

    /// <summary>Null when the project does not exist or the viewer cannot see it.</summary>
    Task<ProjectDetailsDto?> GetDetailsAsync(Guid id, Viewer viewer, CancellationToken cancellationToken);

    /// <summary>Case-insensitive name check among the owner's live projects.</summary>
    Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? excludingProjectId, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracked load, for removal.</summary>
    Task<ProjectMember?> GetMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);

    void Add(Project project);

    /// <summary>Marks for deletion; the soft-delete interceptor turns it into a soft delete of the project and its tasks.</summary>
    void Remove(Project project);

    void AddMember(ProjectMember member);

    void RemoveMember(ProjectMember member);
}
