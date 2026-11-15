using FluentValidation;

namespace SmartTaskManager.Application.Common.Paging;

/// <summary>Validates the shared paging fields; derived validators add rules for their own filters.</summary>
public abstract class PagedQueryValidator<T> : AbstractValidator<T>
    where T : PagedQuery
{
    protected PagedQueryValidator(IReadOnlyCollection<string> sortFields, int maxPageSize = PagedQuery.MaxPageSize)
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, maxPageSize);
        RuleFor(x => x.Search).MaximumLength(PagedQuery.MaxSearchLength);
        RuleFor(x => x.SortDirection).IsInEnum();
        RuleFor(x => x.SortBy)
            .Must(s => s is null || sortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", sortFields)}.");
    }
}
