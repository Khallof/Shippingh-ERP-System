namespace ShippingERP.Application.DTOs.Common;

/// <summary>
/// Common query-string parameters for list endpoints:
/// ?pageNumber=1&amp;pageSize=20&amp;search=ahmed&amp;sortBy=fullName&amp;sortDirection=asc
/// </summary>
public abstract class PagedQuery
{
    public const int MaxPageSize = 100;

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    /// <summary>Free-text search (each list decides which columns are searched).</summary>
    public string? Search { get; set; }

    /// <summary>Column name to sort by (each list documents its allowed values).</summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" or "desc" (default asc).</summary>
    public string? SortDirection { get; set; }

    /// <summary>True when SortDirection is "desc". A method (not a property) so it is not shown as a query parameter.</summary>
    public bool IsDescending() =>
        string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}
