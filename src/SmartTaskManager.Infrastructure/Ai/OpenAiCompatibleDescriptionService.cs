using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using SmartTaskManager.Application.Abstractions;
using SmartTaskManager.Application.Common.Exceptions;

namespace SmartTaskManager.Infrastructure.Ai;

/// <summary>
/// Improves descriptions with a chat model behind any OpenAI-compatible endpoint (configured: Google
/// Gemini) via the OpenAI SDK. Provider failures become <see cref="ExternalServiceException"/> with a
/// user-safe message. Neither the prompt, the user's text nor the API key is ever logged.
/// </summary>
internal sealed partial class OpenAiCompatibleDescriptionService : IAiDescriptionService
{
    private const string Source = "ai";
    private const float Temperature = 0.3f; // low: rewriting, not brainstorming
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly AiSettings _settings;
    private readonly ChatClient _client;
    private readonly ILogger<OpenAiCompatibleDescriptionService> _logger;

    public OpenAiCompatibleDescriptionService(IOptions<AiSettings> options, ILogger<OpenAiCompatibleDescriptionService> logger)
    {
        _settings = options.Value;
        _logger = logger;

        var clientOptions = new OpenAIClientOptions
        {
            // The SDK appends "/chat/completions"; a trailing slash would produce "//chat/completions".
            Endpoint = new Uri(_settings.Endpoint.TrimEnd('/')),
            // Our own timeout (below) fires first; this is a backstop for the HTTP pipeline.
            NetworkTimeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds + 5),
            // No automatic retries: retrying a 429 immediately only makes it worse, and the user is waiting.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        _client = new ChatClient(_settings.Model, new ApiKeyCredential(_settings.ApiKey!), clientOptions);

        LogConfigured(_logger, _settings.Model, _settings.Endpoint, _settings.TimeoutSeconds);
    }

    public async Task<AiDescriptionResult> ImproveAsync(string? title, string description, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        ChatMessage[] messages =
        [
            new SystemChatMessage(DescriptionPrompts.System),
            new UserChatMessage(DescriptionPrompts.BuildUserMessage(title, description)),
        ];
        var options = new ChatCompletionOptions
        {
            Temperature = Temperature,
            MaxOutputTokenCount = _settings.MaxOutputTokens,
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var completion = await CompleteWithRetryOn503Async(messages, options, timeout.Token);
            LogCompleted(_logger, completion.FinishReason, stopwatch.ElapsedMilliseconds);

            if (completion.FinishReason == ChatFinishReason.ContentFilter)
            {
                throw new ExternalServiceException(502, "The AI service declined to process this text.");
            }

            if (completion.FinishReason == ChatFinishReason.Length)
            {
                throw new ExternalServiceException(502, "The AI service returned an incomplete suggestion. Please try again.");
            }

            var text = string.Concat(completion.Content
                .Where(part => part.Kind == ChatMessageContentPartKind.Text)
                .Select(part => part.Text));
            return new AiDescriptionResult(text, Source, string.IsNullOrEmpty(completion.Model) ? _settings.Model : completion.Model);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            var retryAfter = ReadRetryAfter(ex);
            LogRateLimited(_logger, retryAfter?.TotalSeconds);
            var wait = retryAfter is { } r
                ? $"in {Math.Ceiling(r.TotalSeconds).ToString(CultureInfo.InvariantCulture)} seconds"
                : "in a moment";
            throw new ExternalServiceException(
                429,
                $"The AI service is receiving too many requests. Please try again {wait}.",
                retryAfter);
        }
        catch (ClientResultException ex)
        {
            if (ex.Status is 401 or 403)
            {
                LogAuthFailed(_logger, ex.Status);
            }
            else
            {
                LogProviderError(_logger, ex.Status, stopwatch.ElapsedMilliseconds);
            }

            throw Unavailable();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(_logger, _settings.TimeoutSeconds);
            throw new ExternalServiceException(504, "The AI service took too long to respond. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            LogTransportError(_logger, ex.GetType().Name);
            throw Unavailable();
        }
        catch (Exception ex) when (ex is not ExternalServiceException and not OperationCanceledException)
        {
            // Boundary to an external system: e.g. a 200 response the SDK cannot parse.
            LogUnexpectedFailure(_logger, ex);
            throw Unavailable();
        }
    }

    /// <summary>
    /// One retry, after a short pause, when the provider answers 503 (temporarily overloaded); that is
    /// usually transient. Timeouts, 429s and other errors are not retried. Both attempts share the
    /// caller's overall timeout, so a retry can never extend the request beyond it.
    /// </summary>
    private async Task<ChatCompletion> CompleteWithRetryOn503Async(
        ChatMessage[] messages,
        ChatCompletionOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client.CompleteChatAsync(messages, options, cancellationToken);
        }
        catch (ClientResultException ex) when (ex.Status == 503)
        {
            LogRetryingAfter503(_logger, RetryDelay.TotalMilliseconds);
            await Task.Delay(RetryDelay, cancellationToken);
            return await _client.CompleteChatAsync(messages, options, cancellationToken);
        }
    }

    private static ExternalServiceException Unavailable() =>
        new(503, "The AI service is currently unavailable. Please try again later.");

    /// <summary>Reads Retry-After as seconds or an HTTP date; null when absent or unreadable.</summary>
    private static TimeSpan? ReadRetryAfter(ClientResultException ex)
    {
        if (ex.GetRawResponse() is not { } response || !response.Headers.TryGetValue("Retry-After", out var value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at - DateTimeOffset.UtcNow is { TotalSeconds: > 0 } delta ? delta : TimeSpan.Zero
            : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AI descriptions use model {Model} at {Endpoint} (timeout {TimeoutSeconds}s)")]
    private static partial void LogConfigured(ILogger logger, string model, string endpoint, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "AI completion finished ({FinishReason}) in {ElapsedMs} ms")]
    private static partial void LogCompleted(ILogger logger, ChatFinishReason finishReason, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI provider rate limit reached; retry after {RetryAfterSeconds}s")]
    private static partial void LogRateLimited(ILogger logger, double? retryAfterSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI provider rejected the credentials ({Status}); check AiSettings:ApiKey")]
    private static partial void LogAuthFailed(ILogger logger, int status);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI provider returned {Status} after {ElapsedMs} ms")]
    private static partial void LogProviderError(ILogger logger, int status, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI provider did not respond within {TimeoutSeconds}s")]
    private static partial void LogTimedOut(ILogger logger, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI provider returned 503; retrying once in {DelayMs} ms")]
    private static partial void LogRetryingAfter503(ILogger logger, double delayMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI provider could not be reached ({ErrorType})")]
    private static partial void LogTransportError(ILogger logger, string errorType);

    [LoggerMessage(Level = LogLevel.Error, Message = "AI provider call failed unexpectedly")]
    private static partial void LogUnexpectedFailure(ILogger logger, Exception exception);
}
