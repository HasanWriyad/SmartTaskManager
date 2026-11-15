using System.ComponentModel.DataAnnotations;

namespace SmartTaskManager.Infrastructure.Ai;

/// <summary>
/// AI provider settings for an OpenAI-compatible chat endpoint (configured: Google Gemini).
/// <see cref="ApiKey"/> comes from user-secrets or the <c>AiSettings__ApiKey</c> environment variable;
/// without it the offline fallback is used.
/// </summary>
public sealed class AiSettings
{
    public const string SectionName = "AiSettings";

    /// <summary>OpenAI-compatible base URL. Gemini: https://generativelanguage.googleapis.com/v1beta/openai/.</summary>
    [Required]
    [Url]
    public string Endpoint { get; init; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    /// <summary>Model id as the endpoint expects it, e.g. "gemini-3.5-flash-lite".</summary>
    [Required]
    public string Model { get; init; } = "gemini-3.5-flash-lite";

    /// <summary>Provider API key. Never logged.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Upper bound for one improvement request, end to end (including the single 503 retry).</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Caps the response length (about 4 characters per token). For models that "think", hidden
    /// reasoning tokens count against this cap too; see <see cref="ReasoningEffort"/>.
    /// </summary>
    [Range(100, 2000)]
    public int MaxOutputTokens { get; init; } = 1500;

    /// <summary>
    /// Sent as <c>reasoning_effort</c>: none, minimal, low, medium or high (models differ: gemini-3.5-flash-lite
    /// rejects "none"). Default "minimal": rewriting a description needs little hidden reasoning, which otherwise
    /// consumes the output budget and adds latency. Empty means the parameter is not sent (the model's default applies).
    /// </summary>
    // Whitespace around (or instead of) the value is allowed, matching how the value is trimmed when used.
    [RegularExpression(@"^\s*(none|minimal|low|medium|high)?\s*$")]
    public string? ReasoningEffort { get; init; } = "minimal";

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
