using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cod;

/// <summary>
/// تسويات التحصيل وعهدة السائقين (COD and driver cash adjustments): shortage write-off, excess cash, collection
/// correction or reversal, extra sender fee, sender compensation. Requested with COD.Adjust and a reason;
/// approved or rejected by ANOTHER user with COD.ApproveAdjustment. Nothing changes before approval.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cod/adjustments")]
[HasPermission(Permissions.Cod.View)]
public sealed class CodAdjustmentsController : ApiControllerBase
{
    private readonly ICodAdjustmentService _adjustments;

    public CodAdjustmentsController(ICodAdjustmentService adjustments)
    {
        _adjustments = adjustments;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CodAdjustmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CodAdjustmentDto>>>> GetAll(
        [FromQuery] CodAdjustmentListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _adjustments.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CodAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CodAdjustmentDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _adjustments.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Cod.Adjust)]
    [ProducesResponseType(typeof(ApiResponse<CodAdjustmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CodAdjustmentDto>>> Create(
        [FromBody] CreateCodAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var adjustment = await _adjustments.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = adjustment.Id, version = "1" }, adjustment, "تم تسجيل طلب التسوية بانتظار الموافقة");
    }

    [HttpPost("{id:long}/approve")]
    [HasPermission(Permissions.Cod.ApproveAdjustment)]
    [ProducesResponseType(typeof(ApiResponse<CodAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CodAdjustmentDto>>> Approve(
        long id,
        [FromBody] DecideCodAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _adjustments.ApproveAsync(id, request, cancellationToken), "تم اعتماد التسوية وتطبيقها");
    }

    [HttpPost("{id:long}/reject")]
    [HasPermission(Permissions.Cod.ApproveAdjustment)]
    [ProducesResponseType(typeof(ApiResponse<CodAdjustmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CodAdjustmentDto>>> Reject(
        long id,
        [FromBody] DecideCodAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _adjustments.RejectAsync(id, request, cancellationToken), "تم رفض التسوية");
    }
}
