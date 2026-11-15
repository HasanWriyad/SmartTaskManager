namespace SmartTaskManager.Domain.Common;

/// <summary>Entity with creation and last-modification timestamps (UTC), maintained by the persistence layer.</summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void MarkCreated(DateTimeOffset at) => CreatedAt = at;

    public void MarkUpdated(DateTimeOffset at) => UpdatedAt = at;
}
