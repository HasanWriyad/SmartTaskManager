namespace SmartTaskManager.Domain.Enums;

/// <summary>Stored as int so that sorting by priority follows severity order.</summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}
