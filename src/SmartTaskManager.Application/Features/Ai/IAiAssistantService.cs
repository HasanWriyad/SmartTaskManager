namespace SmartTaskManager.Application.Features.Ai;

public interface IAiAssistantService
{
    /// <summary>Returns an improved description as a suggestion. Nothing is persisted.</summary>
    Task<ImproveDescriptionResponse> ImproveDescriptionAsync(ImproveDescriptionRequest request, CancellationToken cancellationToken);
}
