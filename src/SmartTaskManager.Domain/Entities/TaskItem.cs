using SmartTaskManager.Domain.Common;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Domain.Entities;

/// <summary>A task within a project. Named TaskItem to avoid clashing with System.Threading.Tasks.Task.</summary>
public sealed class TaskItem : SoftDeletableEntity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private TaskItem()
    {
    }

    public Guid ProjectId { get; private set; }

    public Project Project { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public User? Assignee { get; private set; }

    public Guid CreatedById { get; private set; }

    public User CreatedBy { get; private set; } = null!;

    public DateTimeOffset? CompletedAt { get; private set; }

    public static TaskItem Create(
        Guid projectId,
        string title,
        string? description,
        TaskPriority priority,
        DateOnly? dueDate,
        Guid createdById) => new()
    {
        ProjectId = projectId,
        Title = title.Trim(),
        Description = NormalizeDescription(description),
        Status = TaskItemStatus.ToDo,
        Priority = priority,
        DueDate = dueDate,
        CreatedById = createdById,
    };

    public void Update(string title, string? description, TaskPriority priority, DateOnly? dueDate)
    {
        Title = title.Trim();
        Description = NormalizeDescription(description);
        Priority = priority;
        DueDate = dueDate;
    }

    /// <summary>Assigns the task to a user, or unassigns it when <paramref name="assigneeId"/> is null.</summary>
    public void AssignTo(Guid? assigneeId) => AssigneeId = assigneeId;

    /// <summary>
    /// Changes the status. A cancelled task can only be reopened to <see cref="TaskItemStatus.ToDo"/>.
    /// Completing sets <see cref="CompletedAt"/>; any other status clears it.
    /// Who may perform the transition is checked by the application layer.
    /// </summary>
    public void ChangeStatus(TaskItemStatus newStatus, DateTimeOffset now)
    {
        if (newStatus == Status)
        {
            return;
        }

        if (Status == TaskItemStatus.Cancelled && newStatus != TaskItemStatus.ToDo)
        {
            throw new DomainException("A cancelled task can only be reopened to To Do.");
        }

        Status = newStatus;
        CompletedAt = newStatus == TaskItemStatus.Completed ? now : null;
    }

    public bool IsReopening(TaskItemStatus newStatus) =>
        Status == TaskItemStatus.Cancelled && newStatus == TaskItemStatus.ToDo;

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
