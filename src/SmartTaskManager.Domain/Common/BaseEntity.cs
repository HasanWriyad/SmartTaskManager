namespace SmartTaskManager.Domain.Common;

/// <summary>Base type for all entities. EF Core generates sequential GUID keys on insert.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
}
