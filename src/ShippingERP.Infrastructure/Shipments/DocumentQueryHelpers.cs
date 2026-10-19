using System.Linq.Expressions;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using Microsoft.EntityFrameworkCore;
using ShippingERP.Domain.Common;

namespace ShippingERP.Infrastructure.Shipments;

/// <summary>Paging / sorting / date filter shared by the document lists (manifests, transfers, returns, cases).</summary>
internal static class DocumentQueryHelpers
{
    public static IQueryable<T> ApplyDateRange<T>(IQueryable<T> query, DocumentListQuery filter)
        where T : BaseEntity
    {
        if (filter.FromUtc.HasValue)
        {
            var from = filter.FromUtc.Value;
            query = query.Where(x => x.CreatedAtUtc >= from);
        }

        if (filter.ToUtc.HasValue)
        {
            var to = filter.ToUtc.Value;
            query = query.Where(x => x.CreatedAtUtc < to);
        }

        return query;
    }

    /// <summary>Sort "number" (document number) or "createdAtUtc" (default: newest first), then page.</summary>
    public static async Task<PagedResult<TDto>> ToPagedAsync<T, TDto>(
        IQueryable<T> query,
        DocumentListQuery filter,
        Expression<Func<T, string>> numberSelector,
        Expression<Func<T, TDto>> projection,
        CancellationToken cancellationToken)
        where T : BaseEntity
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var byNumber = string.Equals(filter.SortBy, "number", StringComparison.OrdinalIgnoreCase);

        IOrderedQueryable<T> ordered;
        if (byNumber)
        {
            ordered = filter.IsDescending() ? query.OrderByDescending(numberSelector) : query.OrderBy(numberSelector);
        }
        else if (string.IsNullOrWhiteSpace(filter.SortBy))
        {
            ordered = query.OrderByDescending(x => x.CreatedAtUtc);
        }
        else
        {
            ordered = filter.IsDescending() ? query.OrderByDescending(x => x.CreatedAtUtc) : query.OrderBy(x => x.CreatedAtUtc);
        }

        var items = await ordered
            .ThenByDescending(x => x.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<TDto>(items, filter.PageNumber, filter.PageSize, totalCount);
    }
}
