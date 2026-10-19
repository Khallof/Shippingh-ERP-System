using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.MasterData;

// ---------------- Service type ----------------

public sealed class ServiceTypeDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public string? Description { get; init; }
    public int EstimatedDeliveryDays { get; init; }
    public int SortOrder { get; init; }
}

public sealed class SaveServiceTypeRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string? Description { get; set; }
    public int EstimatedDeliveryDays { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Search: code, names. SortBy: sortOrder (default), code, arabicName.</summary>
public sealed class ServiceTypeListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "sortOrder", "code", "arabicName" };
}

// ---------------- Payment method ----------------

public sealed class PaymentMethodDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public PaymentMethodKind Kind { get; init; }
    public bool RequiresReference { get; init; }
    public int SortOrder { get; init; }
}

public sealed class SavePaymentMethodRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    /// <summary>Cash, CashOnDelivery, BankTransfer, Card, CustomerAccount, Wallet.</summary>
    public PaymentMethodKind Kind { get; set; }
    public bool RequiresReference { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Search: code, names. SortBy: sortOrder (default), code, arabicName.</summary>
public sealed class PaymentMethodListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "sortOrder", "code", "arabicName" };

    public PaymentMethodKind? Kind { get; set; }
}

// ---------------- Tax rate ----------------

public sealed class TaxRateDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public decimal Percentage { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class SaveTaxRateRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    /// <summary>0 to 100, at most 2 decimals. 5 = 5%.</summary>
    public decimal Percentage { get; set; }
    /// <summary>Setting true removes the default flag from the previous default rate.</summary>
    public bool IsDefault { get; set; }
}

/// <summary>Search: code, names. SortBy: code (default), arabicName, percentage.</summary>
public sealed class TaxRateListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "percentage" };
}

// ---------------- Expense category ----------------

public sealed class ExpenseCategoryDto : MasterDataDto
{
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public string? Description { get; init; }
}

public sealed class SaveExpenseCategoryRequest
{
    public string Code { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public string? EnglishName { get; set; }
    public string? Description { get; set; }
}

/// <summary>Search: code, names. SortBy: code (default), arabicName.</summary>
public sealed class ExpenseCategoryListQuery : MasterDataListQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName" };
}
