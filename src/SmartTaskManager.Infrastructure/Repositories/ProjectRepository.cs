using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Projects;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Infrastructure.Persistence;

namespace SmartTaskManager.Infrastructure.Repositories;

// The soft-delete query filter hides deleted projects (and members/tasks of deleted projects) everywhere.
internal sealed class ProjectRepository(AppDbContext db) : IProjectRepository
{
    public Task<Project?> GetVisibleAsync(Guid id, Viewer viewer, CancellationToken cancellationToken) =>
        db.Projects
            .Where(p => p.Id == id)
            .Where(ProjectAccess.VisibleTo(viewer))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectListItemDto>> ListVisibleAsync(Viewer viewer, CancellationToken cancellationToken) =>
        await db.Projects
            .AsNoTracking()
            .Where(ProjectAccess.VisibleTo(viewer))
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Select(ProjectAccess.ToListItem(viewer))
            .ToListAsync(cancellationToken);

    public Task<ProjectDetailsDto?> GetDetailsAsync(Guid id, Viewer viewer, CancellationToken cancellationToken) =>
        db.Projects
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Where(ProjectAccess.VisibleTo(viewer))
            .Select(ProjectAccess.ToDetails(viewer))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? excludingProjectId, CancellationToken cancellationToken) =>
        db.Projects.AnyAsync(
            p => p.OwnerId == ownerId && p.Name == name && p.Id != excludingProjectId,
            cancellationToken);

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

    public Task<ProjectMember?> GetMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken) =>
        db.ProjectMembers.SingleOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

    public void Add(Project project) => db.Projects.Add(project);

    public void Remove(Project project) => db.Projects.Remove(project);

    public void AddMember(ProjectMember member) => db.ProjectMembers.Add(member);

    public void RemoveMember(ProjectMember member) => db.ProjectMembers.Remove(member);
}
