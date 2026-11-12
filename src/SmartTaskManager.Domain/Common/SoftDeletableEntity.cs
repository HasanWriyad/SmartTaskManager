namespace SmartTaskManager.Domain.Common;

/// <summary>Auditable entity that supports soft delete.</summary>
public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedById { get; private set; }

    public void MarkDeleted(DateTimeOffset at, Guid? deletedById)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = at;
        DeletedById = deletedById;
    }
}
