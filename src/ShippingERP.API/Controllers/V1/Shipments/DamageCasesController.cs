using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Shipments;
using ShippingERP.Application.Interfaces.Shipments;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Shipments;

/// <summary>
/// حالات التلف والفقدان. Reading requires Shipments.View; changes require DamageCases.Manage.
/// Evidence photos: POST /shipments/{id}/attachments with attachmentType = DamageEvidence and damageCaseId.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/damage-cases")]
[HasPermission(Permissions.Shipments.View)]
public sealed class DamageCasesController : ApiControllerBase
{
    private readonly IDamageCaseService _service;

    public DamageCasesController(IDamageCaseService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DamageCaseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DamageCaseDto>>>> GetAll(
        [FromQuery] DamageCaseListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<DamageCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DamageCaseDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.DamageCases.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DamageCaseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DamageCaseDto>>> Create(
        [FromBody] CreateDamageCaseRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = created.Id, version = "1" }, created, "تم تسجيل الحالة");
    }

    [HttpPost("{id:long}/investigate")]
    [HasPermission(Permissions.DamageCases.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DamageCaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DamageCaseDto>>> Investigate(
        long id,
        [FromBody] InvestigateDamageCaseRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.StartInvestigationAsync(id, request, cancellationToken), "بدأ التحقيق");
    }

    [HttpPost("{id:long}/resolve")]
    [HasPermission(Permissions.DamageCases.Manage)]
    [ProducesResponseType(typeof(ApiResponse<DamageCaseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DamageCaseDto>>> Resolve(
        long id,
        [FromBody] ResolveDamageCaseRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.ResolveAsync(id, request, cancellationToken), "تم إغلاق الحالة");
    }
}
