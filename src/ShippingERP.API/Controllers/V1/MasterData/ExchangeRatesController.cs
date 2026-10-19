using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.MasterData;

/// <summary>
/// أسعار الصرف (ExchangeRates).
/// Reading only requires a signed-in user; changing requires MasterData.Manage.
/// Rates are history: add a new rate with a new EffectiveFromUtc instead of editing an old one.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/exchange-rates")]
public sealed class ExchangeRatesController : ApiControllerBase
{
    private readonly IExchangeRateService _service;

    public ExchangeRatesController(IExchangeRateService service)
    {
        _service = service;
    }

    /// <summary>List with paging, search, filters and sorting.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ExchangeRateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<ExchangeRateDto>>>> GetAll(
        [FromQuery] ExchangeRateListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>One record by id.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ExchangeRateDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>The rate valid at a moment (default now), by currency codes: GET /exchange-rates/effective?from=USD&amp;to=SYP</summary>
    [HttpGet("effective")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ExchangeRateDto>>> GetEffective(
        [FromQuery, BindRequired] string from,
        [FromQuery, BindRequired] string to,
        [FromQuery] DateTime? atUtc,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetEffectiveRateAsync(from, to, atUtc, cancellationToken));
    }

    /// <summary>Creates a record.</summary>
    [HttpPost]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ExchangeRateDto>>> Create(
        [FromBody] SaveExchangeRateRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تمت إضافة سعر الصرف");
    }

    /// <summary>Updates a record (send all fields).</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ExchangeRateDto>>> Update(
        long id,
        [FromBody] SaveExchangeRateRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.UpdateAsync(id, request, cancellationToken), "تم حفظ التعديلات");
    }

    /// <summary>Activates a record.</summary>
    [HttpPatch("{id:long}/activate")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ExchangeRateDto>>> Activate(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(id, true, cancellationToken), "تم التفعيل");
    }

    /// <summary>Deactivates a record: it stays in lists and history but cannot be chosen for new operations.</summary>
    [HttpPatch("{id:long}/deactivate")]
    [HasPermission(Permissions.MasterData.Manage)]
    [ProducesResponseType(typeof(ApiResponse<ExchangeRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<ExchangeRateDto>>> Deactivate(long id, CancellationToken cancellationToken)
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
