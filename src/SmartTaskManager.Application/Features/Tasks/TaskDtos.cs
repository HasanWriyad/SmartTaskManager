using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Application.Features.Tasks;

/// <param name="Priority">Defaults to Medium when omitted.</param>
/// <param name="AssigneeId">Optional; must be an active member of the project.</param>
public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    TaskPriority? Priority,
    DateOnly? DueDate,
    Guid? AssigneeId);

/// <summary>Replaces the editable fields. A null <paramref name="DueDate"/> clears the due date.</summary>
/// <param name="Priority">Required, so an omitted field is an error rather than a silent reset.</param>
public sealed record UpdateTaskRequest(string Title, string? Description, TaskPriority? Priority, DateOnly? DueDate);

/// <summary>Assigns or reassigns. Unassigning is a separate DELETE so a missing field cannot unassign by accident.</summary>
public sealed record AssignTaskRequest(Guid? AssigneeId);

public sealed record UpdateTaskStatusRequest(TaskItemStatus? Status);

/// <summary>A row of either task list (one project, or all projects the caller can see).</summary>
/// <param name="CanManage">Caller may edit, assign, or delete (drives UI actions).</param>
/// <param name="CanUpdateStatus">Caller may change the status.</param>
public sealed record TaskListItemDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    bool CanManage,
    bool CanUpdateStatus);

/// <param name="CanManage">Caller may edit, assign, or delete (drives UI actions).</param>
/// <param name="CanUpdateStatus">Caller may change the status.</param>
public sealed record TaskDetailsDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid CreatedById,
    string CreatedByName,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool CanManage,
    bool CanUpdateStatus);
