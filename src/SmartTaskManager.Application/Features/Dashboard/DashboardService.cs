using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Dashboard;

internal sealed class DashboardService(
    IDashboardRepository dashboard,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var viewer = currentUser.RequireViewer();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var upcomingUntil = today.AddDays(DashboardDefaults.UpcomingWindowDays);

        // Three small queries; the same DbContext can't run them concurrently, so they run in sequence.
        var totalProjects = await dashboard.CountVisibleProjectsAsync(viewer, cancellationToken);
        var buckets = await dashboard.CountTasksAsync(viewer, today, upcomingUntil, cancellationToken);
        var upcoming = await dashboard.ListUpcomingAsync(
            viewer, today, upcomingUntil, DashboardDefaults.UpcomingListSize, cancellationToken);

        // At most 4 statuses x 4 priorities x 4 buckets x 2 (mine or not) rows: aggregating them here is trivial.
        var byStatus = Enum.GetValues<TaskItemStatus>()
            .Select(s => new StatusCountDto(s, buckets.Where(b => b.Status == s).Sum(b => b.Count)))
            .ToList();
        var byPriority = Enum.GetValues<TaskPriority>()
            .Select(p => new PriorityCountDto(p, buckets.Where(b => b.Priority == p).Sum(b => b.Count)))
            .ToList();

        var pending = buckets.Where(b => IsPending(b.Status)).ToList();
        var myPending = pending.Where(b => b.AssignedToViewer).ToList();

        return new DashboardDto(
            AsOf: today,
            TotalProjects: totalProjects,
            TotalTasks: buckets.Sum(b => b.Count),
            TasksByStatus: byStatus,
            TasksByPriority: byPriority,
            Completion: new CompletionSummaryDto(
                Completed: CountOf(byStatus, TaskItemStatus.Completed),
                Pending: pending.Sum(b => b.Count),
                Cancelled: CountOf(byStatus, TaskItemStatus.Cancelled)),
            OverdueCount: pending.Where(b => b.DueBucket == DueBucket.Overdue).Sum(b => b.Count),
            UpcomingWindowDays: DashboardDefaults.UpcomingWindowDays,
            UpcomingCount: pending.Where(b => b.DueBucket == DueBucket.Upcoming).Sum(b => b.Count),
            UpcomingTasks: upcoming,
            MyTasks: new MyTasksDto(
                Pending: myPending.Sum(b => b.Count),
                Overdue: myPending.Where(b => b.DueBucket == DueBucket.Overdue).Sum(b => b.Count)));
    }

    /// <summary>Pending = not finished: ToDo or InProgress.</summary>
    public static bool IsPending(TaskItemStatus status) =>
        status is TaskItemStatus.ToDo or TaskItemStatus.InProgress;

    private static int CountOf(IEnumerable<StatusCountDto> counts, TaskItemStatus status) =>
        counts.Single(c => c.Status == status).Count;
}
