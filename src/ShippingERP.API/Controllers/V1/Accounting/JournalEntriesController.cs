using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Controllers.V1.Billing;
using ShippingERP.API.Extensions;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// قيود اليومية. Manual entries follow Draft -> Submitted -> Approved (another user) -> Posted; Submitted / Approved
/// can be Rejected back to editing. Posted entries are immutable: they are corrected with a reversal (or a new
/// adjustment entry). Automatic entries (invoices, payments, COD, cash...) appear here too, already posted.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting/journal-entries")]
[HasPermission(Permissions.Accounting.View)]
public sealed class JournalEntriesController : ApiControllerBase
{
    private readonly IJournalEntryService _journal;

    public JournalEntriesController(IJournalEntryService journal)
    {
        _journal = journal;
    }

    /// <summary>
    /// Filters: status, journalType, fromDate / toDate, branchId, accountId, sourceType, isSystemGenerated,
    /// accountingPeriodId. Search: entry number, description, reference, source document number.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<JournalEntryListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<JournalEntryListItemDto>>>> GetAll([FromQuery] JournalEntryListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>
    /// Creates a manual entry (Draft, or Submitted with submit = true). Balanced lines only (400 otherwise); control
    /// accounts of the sub-ledgers (cashboxes, receivables, driver custody, sender COD money) refuse manual lines.
    /// "Idempotency-Key": the same key and body returns the first entry (200 + Idempotent-Replayed).
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.Accounting.CreateJournal)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> Create(
        [FromBody] SaveJournalEntryRequest request,
        [FromHeader(Name = CustomerPaymentsController.IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _journal.CreateAsync(request, idempotencyKey, cancellationToken);
        if (result.Replayed)
        {
            Response.Headers[CustomerPaymentsController.ReplayedHeader] = "true";
            return Ok(ApiResponse<JournalEntryDto>.Ok(result.Entry, "هذا القيد مسجل من قبل بنفس مفتاح Idempotency-Key", HttpContext.GetTraceId()));
        }

        return CreatedResponse(nameof(GetById), new { id = result.Entry.Id, version = "1" }, result.Entry, "تم حفظ القيد");
    }

    /// <summary>Replaces a Draft / Rejected entry (header and all lines).</summary>
    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Accounting.CreateJournal)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> Update(long id, [FromBody] SaveJournalEntryRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.UpdateAsync(id, request, cancellationToken), "تم حفظ القيد");
    }

    /// <summary>Deletes a Draft / Rejected entry. Posted entries are never deleted.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Accounting.CreateJournal)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _journal.DeleteAsync(id, cancellationToken);
        return OkMessage("تم حذف القيد");
    }

    [HttpPost("{id:long}/submit")]
    [HasPermission(Permissions.Accounting.CreateJournal)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> Submit(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.SubmitAsync(id, cancellationToken), "تم تقديم القيد للاعتماد");
    }

    /// <summary>Approves a submitted entry. Four eyes: the author / submitter cannot approve it (Accounting:AllowSelfApproval).</summary>
    [HttpPost("{id:long}/approve")]
    [HasPermission(Permissions.Accounting.ApproveJournal)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> Approve(long id, [FromBody] JournalDecisionRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.ApproveAsync(id, request, cancellationToken), "تم اعتماد القيد");
    }

    [HttpPost("{id:long}/reject")]
    [HasPermission(Permissions.Accounting.ApproveJournal)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> Reject(long id, [FromBody] JournalReasonRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.RejectAsync(id, request, cancellationToken), "تم رفض القيد");
    }

    /// <summary>Posts an approved entry into the general ledger (JV number). 422 when its period is closed.</summary>
    [HttpPost("{id:long}/post")]
    [HasPermission(Permissions.Accounting.PostJournal)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> Post(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.PostAsync(id, cancellationToken), "تم ترحيل القيد");
    }

    /// <summary>
    /// Reverses a posted manual entry: a new posted entry with debits and credits swapped (reason required, date in an
    /// open period). The original becomes Reversed; both stay in the ledger. Returns the reversal entry.
    /// </summary>
    [HttpPost("{id:long}/reverse")]
    [HasPermission(Permissions.Accounting.ReverseJournal)]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> Reverse(long id, [FromBody] ReverseJournalEntryRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _journal.ReverseAsync(id, request, cancellationToken), "تم عكس القيد");
    }
}
