using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Common.Paging;
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

    public Task<PagedResult<ProjectListItemDto>> ListVisibleAsync(
        Viewer viewer,
        ProjectListQuery query,
        CancellationToken cancellationToken)
    {
        var projects = db.Projects.AsNoTracking().Where(ProjectAccess.VisibleTo(viewer));

        projects = Filter(projects, query, viewer.UserId);

        var direction = query.SortDirection;
        var ordered = query switch
        {
            _ when query.IsSortField(ProjectSortFields.CreatedAt) => projects.OrderByDirection(p => p.CreatedAt, direction),
            _ when query.IsSortField(ProjectSortFields.TaskCount) => projects
                .OrderByDirection(p => p.Tasks.Count(), direction) // live tasks only (query filter)
                .ThenBy(p => p.Name),
            _ => projects.OrderByDirection(p => p.Name, direction), // default
        };

        return ordered
            .ThenBy(p => p.Id)
            .Select(ProjectAccess.ToListItem(viewer))
            .ToPagedResultAsync(query, cancellationToken);
    }

    private static IQueryable<Project> Filter(IQueryable<Project> projects, ProjectListQuery query, Guid viewerId)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            projects = projects.Where(p =>
                p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
        }

        if (query.OwnerId is { } ownerId)
        {
            projects = projects.Where(p => p.OwnerId == ownerId);
        }

        if (query.OwnedByMe is { } ownedByMe)
        {
            projects = ownedByMe
                ? projects.Where(p => p.OwnerId == viewerId)
                : projects.Where(p => p.OwnerId != viewerId);
        }

        // Dates are whole UTC days: [from 00:00, to + 1 day 00:00).
        if (query.CreatedFrom is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            projects = projects.Where(p => p.CreatedAt >= start);
        }

        if (query.CreatedTo is { } to)
        {
            var endExclusive = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            projects = projects.Where(p => p.CreatedAt < endExclusive);
        }

        return projects;
    }

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
