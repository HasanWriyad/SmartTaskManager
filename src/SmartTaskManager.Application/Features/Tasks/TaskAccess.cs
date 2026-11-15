using System.Linq.Expressions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Projects;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Features.Tasks;

/// <summary>
/// Task rules from the role-permission matrix. Seeing a task requires seeing its project.
/// <list type="bullet">
/// <item>Manage (create, edit, assign, delete, any status change): Admin, or the ProjectManager who owns the project.</item>
/// <item>Status only: the task's assignee, whatever their role, while active; except reopening a cancelled task.</item>
/// </list>
/// </summary>
public static class TaskAccess
{
    /// <summary>Same as managing the project.</summary>
    public static bool CanManage(Viewer viewer, Guid projectOwnerId) => ProjectAccess.CanManage(viewer, projectOwnerId);

    /// <summary>The caller is the task's assignee (inactive users are already rejected at authentication).</summary>
    public static bool IsAssignee(Viewer viewer, Guid? assigneeId) => assigneeId == viewer.UserId;

    /// <summary>
    /// List row for the given viewer. CanManage and CanUpdateStatus are the same rules as
    /// <see cref="CanManage"/> and <see cref="IsAssignee"/>, written inline so EF translates them into
    /// the list query (no extra round trip per row); <see cref="ToDetails"/> uses the same expressions.
    /// </summary>
    public static Expression<Func<TaskItem, TaskListItemDto>> ToListItem(Viewer viewer)
    {
        var userId = viewer.UserId;
        var isAdmin = viewer.IsAdmin;
        var isProjectManager = viewer.IsProjectManager;
        return t => new TaskListItemDto(
            t.Id,
            t.ProjectId,
            t.Project.Name,
            t.Title,
            t.Status,
            t.Priority,
            t.DueDate,
            t.AssigneeId,
            t.Assignee != null ? t.Assignee.FullName : null,
            t.CompletedAt,
            t.CreatedAt,
            isAdmin || (isProjectManager && t.Project.OwnerId == userId),
            isAdmin || (isProjectManager && t.Project.OwnerId == userId) || t.AssigneeId == userId);
    }

    public static Expression<Func<TaskItem, TaskDetailsDto>> ToDetails(Viewer viewer)
    {
        var userId = viewer.UserId;
        var isAdmin = viewer.IsAdmin;
        var isProjectManager = viewer.IsProjectManager;
        return t => new TaskDetailsDto(
            t.Id,
            t.ProjectId,
            t.Project.Name,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.DueDate,
            t.AssigneeId,
            t.Assignee != null ? t.Assignee.FullName : null,
            t.CreatedById,
            t.CreatedBy.FullName,
            t.CompletedAt,
            t.CreatedAt,
            t.UpdatedAt,
            isAdmin || (isProjectManager && t.Project.OwnerId == userId),
            isAdmin || (isProjectManager && t.Project.OwnerId == userId) || t.AssigneeId == userId);
    }
}
