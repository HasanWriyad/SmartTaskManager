using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartTaskManager.Application.Common.Paging;

namespace SmartTaskManager.Infrastructure.Persistence;

internal static class QueryableExtensions
{
    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> key,
        SortDirection direction) =>
        direction == SortDirection.Desc ? query.OrderByDescending(key) : query.OrderBy(key);

    public static IOrderedQueryable<T> ThenByDirection<T, TKey>(
        this IOrderedQueryable<T> query,
        Expression<Func<T, TKey>> key,
        SortDirection direction) =>
        direction == SortDirection.Desc ? query.ThenByDescending(key) : query.ThenBy(key);

    /// <summary>True when the requested sort field equals <paramref name="field"/> (case-insensitive).</summary>
    public static bool IsSortField(this PagedQuery query, string field) =>
        string.Equals(query.SortBy, field, StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs a COUNT and a single page query. The query must already be ordered deterministically.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagedQuery paging,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        List<T> items = total == 0
            ? []
            : await query
                .Skip((paging.Page - 1) * paging.PageSize)
                .Take(paging.PageSize)
                .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, paging.Page, paging.PageSize, total);
    }
}
