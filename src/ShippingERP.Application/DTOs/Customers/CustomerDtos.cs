using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Customers;

public sealed class CustomerDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public CustomerType CustomerType { get; init; }
    public CustomerRole Roles { get; init; }
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string? AlternatePhone { get; init; }
    public string? Email { get; init; }
    public string? NationalOrCompanyNumber { get; init; }
    public decimal CreditLimit { get; init; }
    public decimal OpeningBalance { get; init; }
    public string? Notes { get; init; }
    public long DefaultBranchId { get; init; }
    public string DefaultBranchName { get; init; } = string.Empty;
    public long? PricingPlanId { get; init; }
    public string? PricingPlanName { get; init; }
    public int AddressCount { get; init; }
}

/// <summary>Body of POST /api/v1/customers and PUT /api/v1/customers/{id}.</summary>
public sealed class SaveCustomerRequest
{
    /// <summary>Leave empty on create to get the next automatic code (CUS-00001).</summary>
    public string? Code { get; set; }
    /// <summary>Individual or Business.</summary>
    public CustomerType CustomerType { get; set; } = CustomerType.Individual;
    /// <summary>Sender, Recipient or Both.</summary>
    public CustomerRole Roles { get; set; } = CustomerRole.Both;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string? Email { get; set; }
    public string? NationalOrCompanyNumber { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OpeningBalance { get; set; }
    public string? Notes { get; set; }
    public long DefaultBranchId { get; set; }
    public long? PricingPlanId { get; set; }
}

/// <summary>
/// Search: code, names, phone, alternate phone, e-mail, national/company number.
/// SortBy: code (default), arabicName, englishName, createdAtUtc, creditLimit.
/// </summary>
public sealed class CustomerListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "englishName", "createdAtUtc", "creditLimit" };

    public CustomerType? CustomerType { get; set; }

    /// <summary>Sender = customers that can send (Sender or Both); Recipient = customers that can receive.</summary>
    public CustomerRole? Role { get; set; }

    public long? DefaultBranchId { get; set; }

    public long? PricingPlanId { get; set; }
}

// ---------------- Address ----------------

public sealed class CustomerAddressDto
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public string Label { get; init; } = string.Empty;
    public long CityId { get; init; }
    public string CityName { get; init; } = string.Empty;
    public long? ZoneId { get; init; }
    public string? ZoneName { get; init; }
    public string AddressLine { get; init; } = string.Empty;
    public string? Landmark { get; init; }
    public string? BuildingNumber { get; init; }
    public string? PostalCode { get; init; }
    public string? ContactName { get; init; }
    public string? Phone { get; init; }
    public string? AlternatePhone { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

/// <summary>Body of POST/PUT /api/v1/customers/{customerId}/addresses.</summary>
public sealed class SaveCustomerAddressRequest
{
    public string Label { get; set; } = string.Empty;
    public long CityId { get; set; }
    /// <summary>Optional; must belong to the city.</summary>
    public long? ZoneId { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string? Landmark { get; set; }
    public string? BuildingNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? AlternatePhone { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    /// <summary>True makes this the only default address. The first address is always the default.</summary>
    public bool IsDefault { get; set; }
}
