using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.MasterData;

/// <summary>
/// الموردون (Suppliers).
/// Reading requires Suppliers.View; changing requires Suppliers.Manage.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/suppliers")]
[HasPermission(Permissions.Suppliers.View)]
public sealed class SuppliersController : ApiControllerBase
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service)
    {
        _service = service;
    }

    /// <summary>List with paging, search, filters and sorting.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SupplierDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierDto>>>> GetAll(
        [FromQuery] SupplierListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>One record by id.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Creates a record.</summary>
    [HttpPost]
    [HasPermission(Permissions.Suppliers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Create(
        [FromBody] SaveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تمت إضافة المورد");
    }

    /// <summary>Updates a record (send all fields).</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Suppliers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Update(
        long id,
        [FromBody] SaveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.UpdateAsync(id, request, cancellationToken), "تم حفظ التعديلات");
    }

    /// <summary>Activates a record.</summary>
    [HttpPatch("{id:long}/activate")]
    [HasPermission(Permissions.Suppliers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Activate(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(id, true, cancellationToken), "تم التفعيل");
    }

    /// <summary>Deactivates a record: it stays in lists and history but cannot be chosen for new operations.</summary>
    [HttpPatch("{id:long}/deactivate")]
    [HasPermission(Permissions.Suppliers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Deactivate(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(id, false, cancellationToken), "تم الإيقاف");
    }

    /// <summary>Soft delete. Refused with 422 when other records use it (deactivate it instead).</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Suppliers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return OkMessage("تم الحذف");
    }
}
