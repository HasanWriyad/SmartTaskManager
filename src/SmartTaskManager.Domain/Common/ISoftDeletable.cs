namespace SmartTaskManager.Domain.Common;

/// <summary>
/// Entity that is flagged as deleted instead of being removed from the database.
/// The persistence layer turns a delete into <see cref="MarkDeleted"/> and hides deleted rows with a query filter.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedById { get; }

    void MarkDeleted(DateTimeOffset at, Guid? deletedById);
}
