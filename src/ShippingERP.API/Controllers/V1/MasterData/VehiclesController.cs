using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.MasterData;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.MasterData;

/// <summary>
/// المركبات (Vehicles).
/// Reading requires Vehicles.View; changing requires Vehicles.Manage.
/// Users without Branches.ViewAll only see and manage vehicles of their own branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vehicles")]
[HasPermission(Permissions.Vehicles.View)]
public sealed class VehiclesController : ApiControllerBase
{
    private readonly IVehicleService _service;

    public VehiclesController(IVehicleService service)
    {
        _service = service;
    }

    /// <summary>List with paging, search, filters and sorting.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VehicleDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<VehicleDto>>>> GetAll(
        [FromQuery] VehicleListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    /// <summary>One record by id.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Creates a record.</summary>
    [HttpPost]
    [HasPermission(Permissions.Vehicles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<VehicleDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> Create(
        [FromBody] SaveVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تمت إضافة المركبة");
    }

    /// <summary>Updates a record (send all fields).</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Vehicles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> Update(
        long id,
        [FromBody] SaveVehicleRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.UpdateAsync(id, request, cancellationToken), "تم حفظ التعديلات");
    }

    /// <summary>Activates a record.</summary>
    [HttpPatch("{id:long}/activate")]
    [HasPermission(Permissions.Vehicles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> Activate(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(id, true, cancellationToken), "تم التفعيل");
    }

    /// <summary>Deactivates a record: it stays in lists and history but cannot be chosen for new operations.</summary>
    [HttpPatch("{id:long}/deactivate")]
    [HasPermission(Permissions.Vehicles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> Deactivate(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.SetActiveAsync(id, false, cancellationToken), "تم الإيقاف");
    }

    /// <summary>Soft delete. Refused with 422 when other records use it (deactivate it instead).</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Vehicles.Manage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return OkMessage("تم الحذف");
    }
}
