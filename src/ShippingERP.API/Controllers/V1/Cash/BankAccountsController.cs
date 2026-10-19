using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.Interfaces.Cash;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Cash;

/// <summary>الحسابات البنكية للشركة (company bank accounts) for cash deposits and bank settlements.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/bank-accounts")]
[HasPermission(Permissions.Cashboxes.View)]
public sealed class BankAccountsController : ApiControllerBase
{
    private readonly IBankAccountService _bankAccounts;

    public BankAccountsController(IBankAccountService bankAccounts)
    {
        _bankAccounts = bankAccounts;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BankAccountDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<BankAccountDto>>>> GetAll([FromQuery] BankAccountListQuery query, CancellationToken cancellationToken)
    {
        return OkResponse(await _bankAccounts.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _bankAccounts.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    [HasPermission(Permissions.Cashboxes.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> Create([FromBody] SaveBankAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await _bankAccounts.CreateAsync(request, cancellationToken);
        return CreatedResponse(nameof(GetById), new { id = account.Id, version = "1" }, account, "تم إنشاء الحساب البنكي");
    }

    [HttpPut("{id:long}")]
    [HasPermission(Permissions.Cashboxes.Manage)]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BankAccountDto>>> Update(long id, [FromBody] SaveBankAccountRequest request, CancellationToken cancellationToken)
    {
        return OkResponse(await _bankAccounts.UpdateAsync(id, request, cancellationToken), "تم حفظ الحساب البنكي");
    }

    /// <summary>Soft delete of an unused bank account (step 7). A used account can only be deactivated.</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(Permissions.Cashboxes.Manage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        await _bankAccounts.DeleteAsync(id, cancellationToken);
        return OkMessage("تم حذف الحساب البنكي");
    }
}
