using SmartTaskManager.Application.Common;
using SmartTaskManager.Application.Features.Dashboard;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Abstractions;

/// <summary>Aggregate queries over the projects (and their live tasks) visible to a viewer.</summary>
public interface IDashboardRepository
{
    Task<int> CountVisibleProjectsAsync(Viewer viewer, CancellationToken cancellationToken);

    /// <summary>
    /// Task counts grouped by status, priority, due-date bucket and whether the task is assigned
    /// to the viewer, computed in one SQL GROUP BY.
    /// </summary>
    Task<IReadOnlyList<TaskBucketCount>> CountTasksAsync(
        Viewer viewer,
        DateOnly today,
        DateOnly upcomingUntil,
        CancellationToken cancellationToken);

    /// <summary>Pending tasks due between <paramref name="today"/> and <paramref name="upcomingUntil"/>, soonest first.</summary>
    Task<IReadOnlyList<UpcomingTaskDto>> ListUpcomingAsync(
        Viewer viewer,
        DateOnly today,
        DateOnly upcomingUntil,
        int take,
        CancellationToken cancellationToken);
}

/// <summary>Where a task's due date falls relative to today.</summary>
public enum DueBucket
{
    NoDueDate = 0,
    Overdue = 1,   // before today
    Upcoming = 2,  // today .. upcomingUntil (inclusive)
    Later = 3,     // after upcomingUntil
}

/// <param name="AssignedToViewer">The task's assignee is the viewer.</param>
public sealed record TaskBucketCount(
    TaskItemStatus Status,
    TaskPriority Priority,
    DueBucket DueBucket,
    bool AssignedToViewer,
    int Count);
