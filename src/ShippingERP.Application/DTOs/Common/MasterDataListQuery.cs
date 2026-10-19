namespace ShippingERP.Application.DTOs.Common;

/// <summary>
/// Paging + search + sort + active filter shared by all master-data lists.
/// Example: ?pageNumber=1&amp;pageSize=20&amp;search=دمشق&amp;isActive=true&amp;sortBy=code&amp;sortDirection=asc
/// </summary>
public abstract class MasterDataListQuery : PagedQuery
{
    /// <summary>true = active only, false = inactive only, empty = both.</summary>
    public bool? IsActive { get; set; }
}
