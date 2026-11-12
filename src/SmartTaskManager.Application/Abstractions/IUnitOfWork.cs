namespace SmartTaskManager.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists tracked changes in one transaction. A unique-key violation is raised as
    /// <see cref="Common.Exceptions.ConflictException"/>.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
