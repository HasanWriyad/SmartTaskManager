using FluentValidation;
using SmartTaskManager.Domain.Entities;

namespace SmartTaskManager.Application.Features.Ai;

public static class AiLimits
{
    /// <summary>Longest description accepted for improvement.</summary>
    public const int MaxInputLength = 2000;

    /// <summary>Suggestions shorter than this are treated as unusable.</summary>
    public const int MinOutputLength = 20;

    /// <summary>A suggestion must fit in a task description so it can be applied as-is.</summary>
    public const int MaxOutputLength = TaskItem.DescriptionMaxLength;
}

/// <param name="Description">The text to improve (up to 2,000 characters).</param>
/// <param name="Title">Optional task title, given to the model as context only.</param>
public sealed record ImproveDescriptionRequest(string Description, string? Title);

/// <param name="ImprovedDescription">Suggested text. Nothing is saved; apply it with a normal task update.</param>
/// <param name="Source">"ai" when produced by the model, "fallback" when the offline stub was used.</param>
/// <param name="Model">The model used, when <paramref name="Source"/> is "ai".</param>
public sealed record ImproveDescriptionResponse(string ImprovedDescription, string Source, string? Model);

internal sealed class ImproveDescriptionRequestValidator : AbstractValidator<ImproveDescriptionRequest>
{
    public ImproveDescriptionRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(AiLimits.MaxInputLength);
        RuleFor(x => x.Title).MaximumLength(TaskItem.TitleMaxLength);
    }
}
