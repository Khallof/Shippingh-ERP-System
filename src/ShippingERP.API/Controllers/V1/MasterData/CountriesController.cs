using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.MasterData;

/// <summary>
/// الدول (Countries).
/// Reading only requires a signed-in user; changing requires MasterData.Manage.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/countries")]
public sealed class CountriesController : ApiControllerBase
{
    private readonly ICountryService _service;

    public CountriesController(ICountryService service)
    {
        _service = service;
    }

    /// <summary>List with paging, search, filters and sorting.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CountryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CountryDto>>>> GetAll(
        [FromQuery] CountryListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>One record by id.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CountryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CountryDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Creates a record.</summary>
    [HttpPost]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CountryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CountryDto>>> Create(
        [FromBody] SaveCountryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تمت إضافة الدولة");
    }

    /// <summary>Updates a record (send all fields).</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CountryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<CountryDto>>> Update(
        long id,
        [FromBody] SaveCountryRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.UpdateAsync(id, request, cancellationToken), "تم حفظ التعديلات");
    }

    /// <summary>Activates a record.</summary>
    [HttpPatch("{id:long}/activate")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CountryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CountryDto>>> Activate(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(id, true, cancellationToken), "تم التفعيل");
    }

    /// <summary>Deactivates a record: it stays in lists and history but cannot be chosen for new operations.</summary>
    [HttpPatch("{id:long}/deactivate")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CountryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CountryDto>>> Deactivate(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(id, false, cancellationToken), "تم الإيقاف");
    }

    /// <summary>Soft delete. Refused with 422 when other records use it (deactivate it instead).</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return OkMessage("تم الحذف");
    }
}
