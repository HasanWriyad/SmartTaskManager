using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Dashboard;

/// <summary>
/// Statistics over the projects visible to the caller (all projects for an Admin).
/// Pending = ToDo + InProgress; Cancelled counts as neither completed nor pending.
/// Overdue and upcoming only count pending tasks.
/// </summary>
/// <param name="AsOf">The UTC date used as "today" for overdue/upcoming.</param>
/// <param name="TasksByStatus">One entry per status, including zero counts.</param>
/// <param name="TasksByPriority">One entry per priority, including zero counts.</param>
/// <param name="OverdueCount">Pending tasks due before <paramref name="AsOf"/>.</param>
/// <param name="UpcomingCount">Pending tasks due from <paramref name="AsOf"/> to AsOf + <paramref name="UpcomingWindowDays"/>, inclusive.</param>
/// <param name="UpcomingTasks">The soonest upcoming tasks (at most <see cref="DashboardDefaults.UpcomingListSize"/>).</param>
/// <param name="MyTasks">The caller's own tasks (assigned to them) within the same visible projects.</param>
public sealed record DashboardDto(
    DateOnly AsOf,
    int TotalProjects,
    int TotalTasks,
    IReadOnlyList<StatusCountDto> TasksByStatus,
    IReadOnlyList<PriorityCountDto> TasksByPriority,
    CompletionSummaryDto Completion,
    int OverdueCount,
    int UpcomingWindowDays,
    int UpcomingCount,
    IReadOnlyList<UpcomingTaskDto> UpcomingTasks,
    MyTasksDto MyTasks);

/// <param name="Pending">Assigned to the caller and ToDo or InProgress.</param>
/// <param name="Overdue">Of those, due before today.</param>
public sealed record MyTasksDto(int Pending, int Overdue);

public sealed record StatusCountDto(TaskItemStatus Status, int Count);

public sealed record PriorityCountDto(TaskPriority Priority, int Count);

public sealed record CompletionSummaryDto(int Completed, int Pending, int Cancelled);

public sealed record UpcomingTaskDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly DueDate,
    Guid? AssigneeId,
    string? AssigneeName);

public static class DashboardDefaults
{
    public const int UpcomingWindowDays = 7;
    public const int UpcomingListSize = 10;
}
