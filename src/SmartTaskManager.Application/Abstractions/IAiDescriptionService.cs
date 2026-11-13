namespace SmartTaskManager.Application.Abstractions;

/// <summary>
/// Produces an improved task description. Implementations do not persist anything.
/// Failures are raised as <see cref="Common.Exceptions.ExternalServiceException"/>.
/// </summary>
public interface IAiDescriptionService
{
    Task<AiDescriptionResult> ImproveAsync(string? title, string description, CancellationToken cancellationToken);
}

/// <param name="Text">Raw suggestion; the application layer cleans and validates it.</param>
/// <param name="Source">"ai" for a model response, "fallback" for the offline stub.</param>
/// <param name="Model">Model that produced the text, when <paramref name="Source"/> is "ai".</param>
public sealed record AiDescriptionResult(string Text, string Source, string? Model);
