namespace SmartTaskManager.Domain.Entities;

/// <summary>Links a user to a project they can see. Hard-deleted when the member is removed.</summary>
public sealed class ProjectMember
{
    private ProjectMember()
    {
    }

    public Guid ProjectId { get; private set; }

    public Project Project { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public DateTimeOffset AddedAt { get; private set; }

    public static ProjectMember Create(Guid projectId, Guid userId, DateTimeOffset addedAt) => new()
    {
        ProjectId = projectId,
        UserId = userId,
        AddedAt = addedAt,
    };
}
