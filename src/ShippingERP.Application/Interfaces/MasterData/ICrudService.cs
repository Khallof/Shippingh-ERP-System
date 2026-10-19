using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Common;

namespace ShippingERP.Application.Interfaces.MasterData;

/// <summary>
/// Standard operations of a master-data table. Every entity service implements this, so all lists behave
/// the same way (paging, search, sort, active filter, branch restrictions, audit log).
/// </summary>
/// <typeparam name="TDto">What the API returns.</typeparam>
/// <typeparam name="TSave">Body of POST and PUT.</typeparam>
/// <typeparam name="TQuery">Query string of the list endpoint.</typeparam>
public interface ICrudService<TDto, TSave, TQuery>
    where TQuery : MasterDataListQuery
{
    Task<PagedResult<TDto>> GetPagedAsync(TQuery query, CancellationToken cancellationToken);

    Task<TDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<TDto> CreateAsync(TSave request, CancellationToken cancellationToken);

    Task<TDto> UpdateAsync(long id, TSave request, CancellationToken cancellationToken);

    /// <summary>Activate (true) or deactivate (false). Inactive records stay visible but cannot be chosen for new operations.</summary>
    Task<TDto> SetActiveAsync(long id, bool isActive, CancellationToken cancellationToken);

    /// <summary>
    /// Soft delete (the row stays in the database, hidden). Refused with 422 when other records use it;
    /// deactivate it instead.
    /// </summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}
