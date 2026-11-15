using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Domain.Entities;
using SmartTaskManager.Domain.Enums;

namespace SmartTaskManager.Infrastructure.Persistence.Seed;

/// <summary>
/// Fills an empty development database with demo users, projects and tasks so every screen has
/// something to show. Runs only when the caller says the environment is Development, SeedDemo:Enabled
/// is true, SeedDemo:Password is configured, and the database has never had a project (soft-deleted
/// ones included). Everything is saved in one SaveChanges, so a failure leaves nothing behind.
/// </summary>
internal sealed partial class DemoDataSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<SeedDemoOptions> options,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    private const int MinPasswordLength = 12;
    private const string EmailDomain = "demo.smarttask.local";

    private static readonly (string Key, string FullName, UserRole Role)[] DemoUsers =
    [
        ("ayesha", "Ayesha Rahman", UserRole.Admin),
        ("rafi", "Rafi Ahmed", UserRole.ProjectManager),
        ("sadia", "Sadia Karim", UserRole.ProjectManager),
        ("nusrat", "Nusrat Jahan", UserRole.TeamMember),
        ("tanvir", "Tanvir Hasan", UserRole.TeamMember),
        ("imran", "Imran Hossain", UserRole.TeamMember),
        ("farhana", "Farhana Akter", UserRole.TeamMember),
    ];

    private static readonly (string Name, string Description, string Owner, string[] Members)[] DemoProjects =
    [
        ("Customer Portal", "Self-service portal where customers track orders and invoices.", "rafi", ["nusrat", "tanvir", "ayesha"]),
        ("Website Redesign", "New marketing site with a refreshed design system.", "rafi", ["nusrat", "farhana"]),
        ("Billing Service", "Invoicing and payment processing backend.", "sadia", ["tanvir", "imran"]),
        ("Internal Tools", "Admin utilities, CI and observability for the team.", "sadia", ["imran", "farhana", "nusrat"]),
    ];

    /// <summary>Due date offsets are days from today (negative = past); null = no due date.</summary>
    private static readonly (string Project, string Title, string Description, TaskPriority Priority, TaskItemStatus Status, int? DueInDays, string? Assignee)[] DemoTasks =
    [
        ("Customer Portal", "Implement JWT refresh tokens", "Rotate refresh tokens on every use and revoke the chain on reuse.", TaskPriority.High, TaskItemStatus.InProgress, 1, "ayesha"),
        ("Customer Portal", "Role-based access for project admins", "Only owners and admins may manage members.", TaskPriority.High, TaskItemStatus.InProgress, 4, "ayesha"),
        ("Customer Portal", "Fix pagination off-by-one in task list", "The last page repeats the first item of the previous page.", TaskPriority.High, TaskItemStatus.ToDo, -3, "tanvir"),
        ("Customer Portal", "Order history export", "Let customers download their order history as CSV.", TaskPriority.Low, TaskItemStatus.InProgress, 6, "tanvir"),
        ("Customer Portal", "Password strength meter", "Show the password rules while the user types.", TaskPriority.Medium, TaskItemStatus.Completed, -6, "nusrat"),
        ("Website Redesign", "Design project settings page", "Settings for name, description and members.", TaskPriority.Medium, TaskItemStatus.ToDo, 2, "nusrat"),
        ("Website Redesign", "Accessibility pass on forms", "Labels, focus order and error announcements.", TaskPriority.Medium, TaskItemStatus.InProgress, 3, "nusrat"),
        ("Website Redesign", "Landing page copy review", "Review headline and feature copy with marketing.", TaskPriority.Low, TaskItemStatus.Completed, -9, "farhana"),
        ("Website Redesign", "Dark mode for dashboard", "Use the design tokens for both themes.", TaskPriority.Low, TaskItemStatus.Completed, -5, "farhana"),
        ("Website Redesign", "Replace hero illustration", "The current illustration is off-brand.", TaskPriority.Low, TaskItemStatus.Cancelled, null, null),
        ("Website Redesign", "Cookie consent banner", "Required before launch in the EU.", TaskPriority.Critical, TaskItemStatus.ToDo, -1, "farhana"),
        ("Billing Service", "Migrate invoices table", "Move invoices to the new schema without downtime.", TaskPriority.High, TaskItemStatus.Completed, -8, "tanvir"),
        ("Billing Service", "Retry failed card payments", "Retry with backoff before marking the invoice unpaid.", TaskPriority.Critical, TaskItemStatus.InProgress, -2, "imran"),
        ("Billing Service", "VAT rounding rules", "Round per line, not per invoice.", TaskPriority.Medium, TaskItemStatus.ToDo, 12, "imran"),
        ("Billing Service", "Monthly revenue report", "Summary per plan and currency.", TaskPriority.Medium, TaskItemStatus.ToDo, 20, null),
        ("Billing Service", "Switch PDF library", "Dropped: the current library is maintained again.", TaskPriority.Low, TaskItemStatus.Cancelled, null, "tanvir"),
        ("Internal Tools", "Write API docs for task endpoints", "Document filters, sorting and paging.", TaskPriority.Low, TaskItemStatus.InProgress, 3, "imran"),
        ("Internal Tools", "Set up CI pipeline for the API", "Build, run the API checks and publish artifacts.", TaskPriority.Medium, TaskItemStatus.Completed, -10, "farhana"),
        ("Internal Tools", "Health checks and structured logging", "Database health check and Serilog request logging.", TaskPriority.Medium, TaskItemStatus.Completed, -4, "nusrat"),
        ("Internal Tools", "Seed demo data for review", "Development-only data so every screen has content.", TaskPriority.Low, TaskItemStatus.ToDo, 9, "nusrat"),
        ("Internal Tools", "Rotate database credentials", "Quarterly rotation for the staging database.", TaskPriority.High, TaskItemStatus.ToDo, null, null),
    ];

    public async Task SeedAsync(bool isDevelopment, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!isDevelopment || !settings.Enabled)
        {
            return;
        }

        if (await db.Projects.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return; // only an empty database is seeded
        }

        if (string.IsNullOrWhiteSpace(settings.Password) || settings.Password.Length < MinPasswordLength)
        {
            LogPasswordMissing(logger, MinPasswordLength);
            return;
        }

        var emails = DemoUsers.Select(u => EmailFor(u.Key)).ToList();
        if (await db.Users.AnyAsync(u => emails.Contains(u.Email), cancellationToken))
        {
            LogUsersExist(logger);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var users = new Dictionary<string, User>();
        foreach (var (key, fullName, role) in DemoUsers)
        {
            var user = User.Create(EmailFor(key), fullName, role);
            user.SetPasswordHash(passwordHasher.HashPassword(user, settings.Password));
            db.Users.Add(user); // the key is generated on Add
            users[key] = user;
        }

        var projects = new Dictionary<string, Project>();
        foreach (var (name, description, owner, members) in DemoProjects)
        {
            var project = Project.Create(name, description, users[owner].Id);
            db.Projects.Add(project);
            projects[name] = project;

            // The owner is always a member, as when a project is created through the API.
            foreach (var member in members.Prepend(owner).Distinct())
            {
                db.ProjectMembers.Add(ProjectMember.Create(project.Id, users[member].Id, now));
            }
        }

        foreach (var (projectName, title, description, priority, status, dueInDays, assignee) in DemoTasks)
        {
            var project = projects[projectName];
            var task = TaskItem.Create(
                project.Id,
                title,
                description,
                priority,
                dueInDays is { } days ? today.AddDays(days) : null,
                project.OwnerId);
            task.AssignTo(assignee is null ? null : users[assignee].Id);
            task.ChangeStatus(status, now);
            db.Tasks.Add(task);
        }

        await db.SaveChangesAsync(cancellationToken);
        LogDemoSeeded(logger, users.Count, projects.Count, DemoTasks.Length, EmailDomain);
    }

    private static string EmailFor(string key) => $"{key}@{EmailDomain}";

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SeedDemo is enabled but SeedDemo:Password is missing or shorter than {MinLength} characters; skipping demo data.")]
    private static partial void LogPasswordMissing(ILogger logger, int minLength);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo users already exist; skipping demo data.")]
    private static partial void LogUsersExist(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Seeded demo data: {Users} users, {Projects} projects, {Tasks} tasks (users sign in as <name>@{Domain} with SeedDemo:Password).")]
    private static partial void LogDemoSeeded(ILogger logger, int users, int projects, int tasks, string domain);
}
