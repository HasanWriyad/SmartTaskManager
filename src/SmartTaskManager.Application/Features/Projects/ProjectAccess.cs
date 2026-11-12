using System.Linq.Expressions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Features.Projects;

/// <summary>
/// The project rules from the role-permission matrix, as LINQ expressions so the repository applies
/// them in SQL (filtering and the CanManage flag) and the service uses the same rules in memory.
/// <list type="bullet">
/// <item>See: Admin, the owner, or a member.</item>
/// <item>Manage (update, delete, members): Admin, or a ProjectManager who owns the project.</item>
/// <item>Create: Admin or ProjectManager.</item>
/// </list>
/// </summary>
public static class ProjectAccess
{
    public static bool CanCreate(Viewer viewer) => viewer.IsAdmin || viewer.IsProjectManager;

    public static bool CanManage(Viewer viewer, Guid ownerId) =>
        viewer.IsAdmin || (viewer.IsProjectManager && ownerId == viewer.UserId);

    public static Expression<Func<Project, bool>> VisibleTo(Viewer viewer)
    {
        var userId = viewer.UserId;
        var isAdmin = viewer.IsAdmin;
        return p => isAdmin || p.OwnerId == userId || p.Members.Any(m => m.UserId == userId);
    }

    public static Expression<Func<Project, ProjectListItemDto>> ToListItem(Viewer viewer)
    {
        var userId = viewer.UserId;
        var isAdmin = viewer.IsAdmin;
        var isProjectManager = viewer.IsProjectManager;
        return p => new ProjectListItemDto(
            p.Id,
            p.Name,
            p.Description,
            p.OwnerId,
            p.Owner.FullName,
            p.Members.Count(),
            p.Tasks.Count(), // soft-deleted tasks are excluded by the query filter
            isAdmin || (isProjectManager && p.OwnerId == userId),
            p.CreatedAt);
    }

    public static Expression<Func<Project, ProjectDetailsDto>> ToDetails(Viewer viewer)
    {
        var userId = viewer.UserId;
        var isAdmin = viewer.IsAdmin;
        var isProjectManager = viewer.IsProjectManager;
        return p => new ProjectDetailsDto(
            p.Id,
            p.Name,
            p.Description,
            p.OwnerId,
            p.Owner.FullName,
            p.Members
                .OrderBy(m => m.User.FullName)
                .Select(m => new ProjectMemberDto(m.UserId, m.User.FullName, m.User.Email, m.AddedAt))
                .ToList(),
            p.Tasks.Count(),
            isAdmin || (isProjectManager && p.OwnerId == userId),
            p.CreatedAt,
            p.UpdatedAt);
    }
}
