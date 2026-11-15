namespace SmartTaskManager.Api.Configuration;

/// <summary>
/// Origins allowed to call the API from a browser with credentials (the refresh-token cookie).
/// Empty by default: in development the Angular proxy makes calls same-origin, so CORS is only needed
/// when the frontend is served from another origin.
/// </summary>
public sealed class FrontendCorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}
