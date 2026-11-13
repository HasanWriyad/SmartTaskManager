using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SmartTaskManager.Application.Abstractions;

namespace SmartTaskManager.Infrastructure.Ai;

/// <summary>
/// Offline stand-in used when no AI API key is configured, so the feature works in development and demos.
/// It tidies the text (whitespace, capitalization, final punctuation) and appends generic steps.
/// Responses are marked with source "fallback" so the UI can say so.
/// </summary>
internal sealed partial class FallbackDescriptionService : IAiDescriptionService
{
    private const string Steps = """
        Steps:
        1. Review the requirements and confirm the expected outcome.
        2. Break the work into smaller, verifiable pieces.
        3. Implement the changes.
        4. Test the result against the requirements.
        5. Update the task status and share the outcome with the team.
        """;

    public FallbackDescriptionService(ILogger<FallbackDescriptionService> logger) => LogFallbackActive(logger);

    public Task<AiDescriptionResult> ImproveAsync(string? title, string description, CancellationToken cancellationToken)
    {
        var text = Whitespace().Replace(description, " ").Trim();
        if (text.Length > 0)
        {
            text = char.ToUpperInvariant(text[0]) + text[1..];
            if (!text.EndsWith('.') && !text.EndsWith('!') && !text.EndsWith('?'))
            {
                text += ".";
            }
        }

        return Task.FromResult(new AiDescriptionResult($"{text}\n\n{Steps}", "fallback", null));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AiSettings:ApiKey is not set; AI description improvement uses the offline fallback")]
    private static partial void LogFallbackActive(ILogger logger);
}
