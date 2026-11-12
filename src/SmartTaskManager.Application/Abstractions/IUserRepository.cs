using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

public interface IUserRepository
{
    /// <param name="normalizedEmail">Email already normalized with <see cref="User.NormalizeEmail"/>.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    void Add(User user);
}
