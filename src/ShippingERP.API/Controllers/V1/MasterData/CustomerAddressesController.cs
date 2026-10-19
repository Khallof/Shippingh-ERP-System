using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ShippingERP.API.Security;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Customers;
using ShippingERP.Application.Interfaces.MasterData;
using ShippingERP.Domain.Authorization;

namespace ShippingERP.API.Controllers.V1.MasterData;

/// <summary>
/// Addresses of a customer (عناوين العميل). Each address has its own contact name and phones.
/// Reading requires Customers.View; changing requires Customers.Manage.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers/{customerId:long}/addresses")]
[HasPermission(Permissions.Customers.View)]
public sealed class CustomerAddressesController : ApiControllerBase
{
    private readonly ICustomerAddressService _service;

    public CustomerAddressesController(ICustomerAddressService service)
    {
        _service = service;
    }

    /// <summary>All addresses of the customer, default address first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CustomerAddressDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CustomerAddressDto>>>> GetAll(
        long customerId,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetAllAsync(customerId, cancellationToken));
    }

    [HttpGet("{addressId:long}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> GetById(
        long customerId,
        long addressId,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.GetByIdAsync(customerId, addressId, cancellationToken));
    }

    /// <summary>Adds an address. The first address becomes the default automatically.</summary>
    [HttpPost]
    [HasPermission(Permissions.Customers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> Create(
        long customerId,
        [FromBody] SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(customerId, request, cancellationToken);
        return CreatedResponse(
            nameof(GetById),
            new { customerId, addressId = created.Id, version = "1" },
            created,
            "تمت إضافة العنوان");
    }

    /// <summary>Updates an address. isDefault = true makes it the default address.</summary>
    [HttpPut("{addressId:long}")]
    [HasPermission(Permissions.Customers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<CustomerAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CustomerAddressDto>>> Update(
        long customerId,
        long addressId,
        [FromBody] SaveCustomerAddressRequest request,
        CancellationToken cancellationToken)
    {
        return OkResponse(await _service.UpdateAsync(customerId, addressId, request, cancellationToken), "تم حفظ العنوان");
    }

    /// <summary>Soft-deletes an address. If it was the default, the oldest remaining address becomes the default.</summary>
    [HttpDelete("{addressId:long}")]
    [HasPermission(Permissions.Customers.Manage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long customerId,
        long addressId,
        CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(customerId, addressId, cancellationToken);
        return OkMessage("تم حذف العنوان");
    }
}
