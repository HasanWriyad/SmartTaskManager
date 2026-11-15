namespace SmartTaskManager.Api.Authorization;

/// <summary>
/// Authorization policy names. Controllers use these for coarse, role-level checks. Rules about
/// ownership and membership (e.g. "PM may edit only own projects") live in the application services.
/// </summary>
public static class Policies
{
    /// <summary>Any authenticated user with a known role. Also the fallback policy for all endpoints.</summary>
    public const string AnyUser = nameof(AnyUser);

    /// <summary>Admin and ProjectManager: create projects, use the AI endpoint, look up users for assignment.</summary>
    public const string AdminOrProjectManager = nameof(AdminOrProjectManager);

    /// <summary>Admin only: user management (list users, change roles, deactivate).</summary>
    public const string AdminOnly = nameof(AdminOnly);
}
