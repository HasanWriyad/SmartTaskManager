namespace SmartTaskManager.Application.Common.Paging;

public enum SortDirection
{
    Asc,
    Desc,
}

/// <summary>
/// Common list parameters bound from the query string. Each list adds its own filters and
/// declares which <see cref="SortBy"/> values it supports (see <see cref="PagedQueryValidator{T}"/>).
/// </summary>
public abstract class PagedQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    /// <summary>Keyword matched against the list's text fields.</summary>
    public string? Search { get; init; }

    /// <summary>Field to sort by; allowed values depend on the endpoint.</summary>
    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Asc;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}
