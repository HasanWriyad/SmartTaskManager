using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common.Exceptions;

namespace SmartTaskManager.Application.Features.Ai;

public interface IAiAssistantService
{
    /// <summary>Returns an improved description as a suggestion. Nothing is persisted.</summary>
    Task<ImproveDescriptionResponse> ImproveDescriptionAsync(ImproveDescriptionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Calls the configured <see cref="IAiDescriptionService"/> and validates what comes back, whichever
/// implementation produced it. User text and suggestions are never logged, only their lengths.
/// </summary>
internal sealed partial class AiAssistantService(
    IAiDescriptionService aiDescriptionService,
    ILogger<AiAssistantService> logger) : IAiAssistantService
{
    private const string UnusableOutput = "The AI service returned an unusable suggestion. Please try again.";
    private const int BadGateway = 502;

    public async Task<ImproveDescriptionResponse> ImproveDescriptionAsync(
        ImproveDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await aiDescriptionService.ImproveAsync(
            string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            request.Description.Trim(),
            cancellationToken);

        var text = Clean(result.Text);
        var problem = Validate(text);
        if (problem is not null)
        {
            LogRejected(logger, result.Source, problem, result.Text.Length);
            throw new ExternalServiceException(BadGateway, UnusableOutput);
        }

        LogImproved(logger, result.Source, request.Description.Length, text.Length);
        return new ImproveDescriptionResponse(text, result.Source, result.Model);
    }

    /// <summary>Normalizes line endings and removes wrapping code fences or quotes models sometimes add.</summary>
    internal static string Clean(string raw)
    {
        var text = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

        var fence = CodeFence().Match(text);
        if (fence.Success)
        {
            text = fence.Groups["body"].Value.Trim();
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1].Trim();
        }

        return MultipleBlankLines().Replace(text, "\n\n");
    }

    /// <summary>Returns a short reason when the suggestion is unusable, otherwise null.</summary>
    internal static string? Validate(string text) => text switch
    {
        _ when text.Length < AiLimits.MinOutputLength => "too short",
        _ when text.Length > AiLimits.MaxOutputLength => "too long",
        // The prompt's delimiters must never appear in output; if they do, the model is echoing its prompt.
        _ when text.Contains("<task_description", StringComparison.OrdinalIgnoreCase)
            || text.Contains("<task_title", StringComparison.OrdinalIgnoreCase) => "echoed prompt delimiters",
        _ => null,
    };

    [GeneratedRegex(@"^```[a-zA-Z]*\s*\n(?<body>[\s\S]*?)\n?```$")]
    private static partial Regex CodeFence();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLines();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Improved description via {Source}: input {InputLength} chars, output {OutputLength} chars")]
    private static partial void LogImproved(ILogger logger, string source, int inputLength, int outputLength);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rejected AI suggestion from {Source}: {Reason} ({Length} chars)")]
    private static partial void LogRejected(ILogger logger, string source, string reason, int length);
}
