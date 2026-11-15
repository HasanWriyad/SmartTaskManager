namespace SmartTaskManager.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists tracked changes in one transaction. A unique-key violation is raised as
    /// <see cref="Common.Exceptions.ConflictException"/>.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts an explicit transaction for check-then-act operations that must not interleave.
    /// Saves and set-based updates made before commit join it. Disposing without commit rolls back.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
