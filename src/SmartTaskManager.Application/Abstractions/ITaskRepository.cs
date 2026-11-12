using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

/// <summary>Soft-deleted tasks are invisible to every method here.</summary>
public interface ITaskRepository
{
    /// <summary>Tracked load of the project's tasks assigned to the user.</summary>
    Task<List<TaskItem>> GetAssignedInProjectAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
}
