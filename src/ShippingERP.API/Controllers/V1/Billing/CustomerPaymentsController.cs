using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Extensions;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Billing;
using ShippingERP.Application.Interfaces.Billing;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.Billing;

/// <summary>
/// دفعات العملاء / سندات القبض (Customer payments).
///
/// POST accepts the header "Idempotency-Key": repeat a request with the same key (network retry, double click) and
/// the first payment is returned with 200 and the header "Idempotent-Replayed: true" instead of recording the money
/// twice. The same key with a different body gives 409.
/// Branch users only see the payments of their branch.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customer-payments")]
[HasPermission(Permissions.Payments.View)]
public sealed class CustomerPaymentsController : ApiControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    private readonly ICustomerPaymentService _payments;

    public CustomerPaymentsController(ICustomerPaymentService payments)
    {
        _payments = payments;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerPaymentListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerPaymentListItemDto>>>> GetAll(
        [FromQuery] CustomerPaymentListQuery query,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _payments.GetPagedAsync(query, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerPaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerPaymentDto>>> GetById(long id, CancellationToken cancellationToken)
    {
        return OkResponse(await _payments.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Records a payment and allocates it to one or several invoices (partial amounts allowed).</summary>
    [HttpPost]
    [HasPermission(Permissions.Payments.Create)]
    [ProducesResponseType(typeof(ApiResponse<CustomerPaymentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CustomerPaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CustomerPaymentDto>>> Create(
        [FromBody] CreateCustomerPaymentRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _payments.CreateAsync(request, idempotencyKey, cancellationToken);

        if (result.IsReplay)
        {
            Response.Headers[ReplayedHeader] = "true";
            return Ok(ApiResponse<CustomerPaymentDto>.Ok(result.Payment, "هذه الدفعة مسجلة من قبل بنفس مفتاح Idempotency-Key", HttpContext.GetTraceId()));
        }

        return CreatedResponse(nameof(GetById), new { id = result.Payment.Id, version = "1" }, result.Payment, "تم تسجيل الدفعة");
    }

    /// <summary>Allocates the unallocated part of a payment to open invoices.</summary>
    [HttpPost("{id:long}/allocations")]
    [HasPermission(Permissions.Payments.Create)]
    [ProducesResponseType(typeof(ApiResponse<CustomerPaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApiResponse<CustomerPaymentDto>>> Allocate(
        long id,
        [FromBody] AllocatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _payments.AllocateAsync(id, request, cancellationToken), "تم تخصيص الدفعة");
    }
}
