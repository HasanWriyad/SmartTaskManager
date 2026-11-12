namespace SmartTaskManager.Api.Auth;

/// <summary>
/// Writes and reads the refresh token cookie. It is httpOnly (no JavaScript access), Secure (HTTPS only),
/// SameSite=Strict (not sent on cross-site requests, which blocks CSRF), and scoped to /api/auth so it is
/// only sent to refresh and logout, never to the rest of the API.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "stm_refresh";
    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, CreateOptions(expiresAt));

    /// <summary>The browser only removes the cookie if Path, Secure and SameSite match the ones it was set with.</summary>
    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, CreateOptions(expiresAt: null));

    private static CookieOptions CreateOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
