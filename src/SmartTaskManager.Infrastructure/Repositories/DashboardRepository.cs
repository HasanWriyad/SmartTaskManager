using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Dashboard;
using SmartTaskManager.Application.Features.Projects;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;
using SmartTaskManager.Infrastructure.Persistence;

namespace SmartTaskManager.Infrastructure.Repositories;

/// <summary>
/// All queries start from the viewer's visible projects (ProjectAccess.VisibleTo) and go through the
/// Tasks navigation, so soft-deleted projects and tasks are excluded by the query filters.
/// </summary>
internal sealed class DashboardRepository(AppDbContext db) : IDashboardRepository
{
    public Task<int> CountVisibleProjectsAsync(Viewer viewer, CancellationToken cancellationToken) =>
        db.Projects.Where(ProjectAccess.VisibleTo(viewer)).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskBucketCount>> CountTasksAsync(
        Viewer viewer,
        DateOnly today,
        DateOnly upcomingUntil,
        CancellationToken cancellationToken)
    {
        var viewerId = viewer.UserId;
        return await VisibleTasks(viewer)
            .GroupBy(t => new
            {
                t.Status,
                t.Priority,
                // Both translate to SQL CASE expressions inside the GROUP BY.
                DueBucket = t.DueDate == null ? DueBucket.NoDueDate
                    : t.DueDate < today ? DueBucket.Overdue
                    : t.DueDate <= upcomingUntil ? DueBucket.Upcoming
                    : DueBucket.Later,
                AssignedToViewer = t.AssigneeId == viewerId,
            })
            .Select(g => new TaskBucketCount(
                g.Key.Status,
                g.Key.Priority,
                g.Key.DueBucket,
                g.Key.AssignedToViewer,
                g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UpcomingTaskDto>> ListUpcomingAsync(
        Viewer viewer,
        DateOnly today,
        DateOnly upcomingUntil,
        int take,
        CancellationToken cancellationToken) =>
        await VisibleTasks(viewer)
            .Where(t => (t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress)
                && t.DueDate != null && t.DueDate >= today && t.DueDate <= upcomingUntil)
            .OrderBy(t => t.DueDate)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Take(take)
            .Select(t => new UpcomingTaskDto(
                t.Id,
                t.ProjectId,
                t.Project.Name,
                t.Title,
                t.Status,
                t.Priority,
                t.DueDate!.Value,
                t.AssigneeId,
                t.Assignee != null ? t.Assignee.FullName : null))
            .ToListAsync(cancellationToken);

    private IQueryable<TaskItem> VisibleTasks(Viewer viewer) =>
        db.Projects
            .AsNoTracking()
            .Where(ProjectAccess.VisibleTo(viewer))
            .SelectMany(p => p.Tasks);
}
