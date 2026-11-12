using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(User user, string password);

    PasswordVerification VerifyPassword(User user, string providedPassword);
}

public enum PasswordVerification
{
    Failed,
    Success,
    SuccessRehashNeeded,
}
