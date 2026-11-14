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
/// <param name="Source"><see cref="AiSources.Ai"/> for a model response, <see cref="AiSources.Fallback"/> for the offline stub.</param>
/// <param name="Model">Model that produced the text, when <paramref name="Source"/> is <see cref="AiSources.Ai"/>.</param>
public sealed record AiDescriptionResult(string Text, string Source, string? Model);
