using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Accounting;

/// <summary>
/// GET /api/v1/accounting/accounts. Search: code, Arabic / English name.
/// SortBy: code (default), arabicName, accountType.
/// </summary>
public sealed class AccountListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "accountType" };

    public AccountType? AccountType { get; set; }

    public long? AccountCategoryId { get; set; }

    public long? ParentAccountId { get; set; }

    public bool? IsPostable { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class AccountDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public AccountType AccountType { get; init; }
    public string AccountTypeName { get; init; } = string.Empty;
    public NormalBalance NormalBalance { get; init; }
    public string NormalBalanceName { get; init; } = string.Empty;
    public long? AccountCategoryId { get; init; }
    public string? AccountCategoryName { get; init; }
    public long? ParentAccountId { get; init; }
    public string? ParentAccountCode { get; init; }
    public string? ParentAccountName { get; init; }
    public int Level { get; init; }
    public bool IsPostable { get; init; }
    public bool IsSystem { get; init; }
    public bool IsCashAccount { get; init; }
    public bool AllowManualPosting { get; init; }
    public bool RequiresCostCenter { get; init; }
    public long? CurrencyId { get; init; }
    public string? CurrencyCode { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>Body of POST / PUT /api/v1/accounting/accounts.</summary>
public sealed class SaveAccountRequest
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>Must equal the parent's type when a parent is given.</summary>
    public AccountType AccountType { get; set; }

    public long? AccountCategoryId { get; set; }

    public long? ParentAccountId { get; set; }

    /// <summary>False = header account (groups children, no posting).</summary>
    public bool IsPostable { get; set; } = true;

    public bool IsCashAccount { get; set; }

    public bool AllowManualPosting { get; set; } = true;

    public bool RequiresCostCenter { get; set; }

    public long? CurrencyId { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>GET /api/v1/accounting/accounts/tree.</summary>
public sealed class AccountTreeQuery
{
    public bool IncludeInactive { get; set; }

    /// <summary>Adds the base-currency balance of each account (posted entries up to AsOfDate).</summary>
    public bool IncludeBalances { get; set; }

    /// <summary>Default: today.</summary>
    public DateTime? AsOfDate { get; set; }
}

public sealed class AccountTreeNodeDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public string AccountTypeName { get; init; } = string.Empty;
    public int Level { get; init; }
    public bool IsPostable { get; init; }
    public bool IsActive { get; init; }

    /// <summary>Natural balance (positive = normal side), children included. Null when balances were not requested.</summary>
    public decimal? Balance { get; set; }

    public List<AccountTreeNodeDto> Children { get; init; } = new();
}

public sealed class AccountCategoryDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public AccountType AccountType { get; init; }
    public string AccountTypeName { get; init; } = string.Empty;
    public CashFlowActivity CashFlowActivity { get; init; }
    public string CashFlowActivityName { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
    public int AccountCount { get; init; }
}

public sealed class SaveAccountCategoryRequest
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public AccountType AccountType { get; set; }

    public CashFlowActivity CashFlowActivity { get; set; } = CashFlowActivity.Operating;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>GET /api/v1/accounting/dimensions. Search: code, name. SortBy: code (default), arabicName.</summary>
public sealed class AccountingDimensionListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName" };

    public DimensionType? DimensionType { get; set; }

    public long? BranchId { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class AccountingDimensionDto
{
    public long Id { get; init; }
    public DimensionType DimensionType { get; init; }
    public string DimensionTypeName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public long? BranchId { get; init; }
    public string? BranchName { get; init; }
    public long? ParentId { get; init; }
    public string? ParentName { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

public sealed class SaveAccountingDimensionRequest
{
    public DimensionType DimensionType { get; set; } = DimensionType.CostCenter;

    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public long? BranchId { get; set; }

    public long? ParentId { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
