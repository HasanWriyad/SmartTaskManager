namespace SmartTaskManager.Infrastructure.Auth;

/// <summary>
/// Claim names used in access tokens. Inbound claim mapping is disabled, so these short JWT
/// names are what appear on the authenticated principal.
/// </summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
