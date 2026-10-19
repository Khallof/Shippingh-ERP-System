using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Application.Interfaces.Cod;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cod;

/// <summary>
/// تسويات المرسلين (sender settlements): pay senders the net amount of their ready COD shipments.
/// Draft (COD.Settle) -> Approved (COD.ApproveSettlement, another user) -> Paid (COD.PaySettlement, cash or bank).
/// Cancel a draft / approved settlement, or reverse a paid one by mistake. Every action also needs COD.View.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cod/settlements")]
[HasPermission(Permissions.Cod.View)]
public sealed class SenderSettlementsController : ApiControllerBase
{
    private readonly ISenderSettlementService _settlements;

    public SenderSettlementsController(ISenderSettlementService settlements)
    {
        _settlements = settlements;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SenderSettlementListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<SenderSettlementListItemDto>>>> GetAll(
        [FromQuery] SenderSettlementListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _settlements.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<SenderSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<SenderSettlementDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _settlements.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>The COD shipments of a sender that can be settled now (ready, not reserved), oldest first.</summary>
    [HttpGet("candidates")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SettlementCandidateDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SettlementCandidateDto>>>> Candidates(
        [FromQuery] SettlementCandidatesQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _settlements.GetCandidatesAsync(query, cancellationToken));
    }

    /// <summary>Creates a draft settlement (whole or partial amounts per COD).</summary>
    [HttpPost]
    [HasPermission(Permissions.Cod.Settle)]
    [ProducesResponseType(typeof(ApiResponse<SenderSettlementDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<SenderSettlementDto>>> Create(
        [FromBody] CreateSenderSettlementRequest request,
        CancellationToken cancellationToken)
    {
        var settlement = await _settlements.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = settlement.Id, version = "1" }, settlement, "تم إنشاء التسوية");
    }

    [HttpPost("{id:long}/approve")]
    [HasPermission(Permissions.Cod.ApproveSettlement)]
    [ProducesResponseType(typeof(ApiResponse<SenderSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<SenderSettlementDto>>> Approve(
        long id,
        [FromBody] ApproveSenderSettlementRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _settlements.ApproveAsync(id, request, cancellationToken), "تم اعتماد التسوية");
    }

    /// <summary>
    /// Pays an approved settlement: cash method + cashboxId (a payment voucher PMV-... is created) or bank transfer
    /// method + bankAccountId + referenceNumber.
    /// </summary>
    [HttpPost("{id:long}/pay")]
    [HasPermission(Permissions.Cod.PaySettlement)]
    [ProducesResponseType(typeof(ApiResponse<SenderSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<SenderSettlementDto>>> Pay(
        long id,
        [FromBody] PaySenderSettlementRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _settlements.PayAsync(id, request, cancellationToken), "تم دفع التسوية للمرسل");
    }

    /// <summary>Cancels a draft (COD.Settle) or an approved settlement (also COD.ApproveSettlement); the CODs are released.</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(Permissions.Cod.Settle)]
    [ProducesResponseType(typeof(ApiResponse<SenderSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<SenderSettlementDto>>> Cancel(
        long id,
        [FromBody] SettlementReasonRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _settlements.CancelAsync(id, request, cancellationToken), "تم إلغاء التسوية");
    }

    /// <summary>Reverses a paid settlement recorded by mistake: the money comes back and the CODs are payable again.</summary>
    [HttpPost("{id:long}/reverse")]
    [HasPermission(Permissions.Cod.ApproveSettlement)]
    [ProducesResponseType(typeof(ApiResponse<SenderSettlementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<SenderSettlementDto>>> Reverse(
        long id,
        [FromBody] SettlementReasonRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _settlements.ReverseAsync(id, request, cancellationToken), "تم عكس التسوية");
    }

    /// <summary>Sender settlement statement (A4 landscape, Arabic): every shipment with its deductions and the net paid.</summary>
    [HttpGet("{id:long}/pdf")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken cancellationToken)
    {
        return FileDownloadResult(await _settlements.GetPdfAsync(id, cancellationToken));
    }
}
