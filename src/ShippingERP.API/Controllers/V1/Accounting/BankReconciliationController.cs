using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Accounting;
using ShippingERP.Application.Interfaces.Accounting;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Accounting;

/// <summary>
/// التسوية البنكية: the bank statement against the ledger of a bank account. POST saves the statement lines, matches
/// them (explicit pairs, then same amount + reference, then same amount within ±7 days) and calculates the
/// reconciliation; it is completed when the difference is 0 and no statement-only line remains.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/accounting")]
[HasPermission(Permissions.Accounting.BankReconciliation)]
public sealed class BankReconciliationController : ApiControllerBase
{
    private readonly IBankReconciliationService _reconciliations;

    public BankReconciliationController(IBankReconciliationService reconciliations)
    {
        _reconciliations = reconciliations;
    }

    /// <summary>Saved bank statement lines. Filters: bankAccountId, isReconciled, fromDate / toDate.</summary>
    [HttpGet("bank-transactions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BankTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<BankTransactionDto>>>> GetTransactions([FromQuery] BankTransactionListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reconciliations.GetTransactionsAsync(query, cancellationToken));
    }

    [HttpGet("bank-reconciliation")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BankReconciliationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<BankReconciliationDto>>>> GetAll([FromQuery] BankReconciliationListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _reconciliations.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("bank-reconciliation/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<BankReconciliationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BankReconciliationDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _reconciliations.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("bank-reconciliation")]
    [ProducesResponseType(typeof(ApiResponse<BankReconciliationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<BankReconciliationDto>>> Create([FromBody] CreateBankReconciliationRequest request, CancellationToken cancellationToken)
    {
        var reconciliation = await _reconciliations.CreateAsync(request, cancellationToken);
        var message = reconciliation.Status == Domain.Enums.BankReconciliationStatus.Completed
            ? "تمت التسوية البنكية واكتملت"
            : "تم حفظ التسوية البنكية كمسودة";
        return CreatedResponse(nameof(GetById), new { id = reconciliation.Id, version = "1" }, reconciliation, message);
    }

    /// <summary>Recalculates a draft (new ledger entries such as bank fees are matched) and completes it.</summary>
    [HttpPost("bank-reconciliation/{id:long}/complete")]
    [ProducesResponseType(typeof(ApiResponse<BankReconciliationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<BankReconciliationDto>>> Complete(long id, [FromBody] CompleteBankReconciliationRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _reconciliations.CompleteAsync(id, request, cancellationToken), "اكتملت التسوية البنكية");
    }
}
