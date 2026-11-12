using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Infrastructure.Persistence.Seed;

/// <summary>Creates the initial Admin user from configuration when no Admin exists yet.</summary>
internal sealed partial class AdminSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SeedAdminOptions> options,
    ILogger<AdminSeeder> logger)
{
    private const int MinPasswordLength = 12;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
        {
            return;
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Email) || string.IsNullOrWhiteSpace(settings.Password))
        {
            LogSeedNotConfigured(logger);
            return;
        }

        if (settings.Password.Length < MinPasswordLength)
        {
            LogPasswordTooShort(logger, MinPasswordLength);
            return;
        }

        var email = User.NormalizeEmail(settings.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            LogEmailTaken(logger);
            return;
        }

        var admin = User.Create(email, settings.FullName, UserRole.Admin);
        admin.SetPasswordHash(passwordHasher.HashPassword(admin, settings.Password));

        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);

        LogAdminSeeded(logger, admin.Id);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No Admin user exists and SeedAdmin:Email / SeedAdmin:Password are not configured; skipping Admin seeding.")]
    private static partial void LogSeedNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SeedAdmin:Password must be at least {MinLength} characters; skipping Admin seeding.")]
    private static partial void LogPasswordTooShort(ILogger logger, int minLength);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "A non-Admin user already uses the configured SeedAdmin:Email; skipping Admin seeding.")]
    private static partial void LogEmailTaken(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded initial Admin user {UserId}.")]
    private static partial void LogAdminSeeded(ILogger logger, Guid userId);
}
