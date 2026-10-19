using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Billing;

// ------------------------------------------------------------------ customer statement

/// <summary>
/// GET /api/v1/customers/{id}/statement?currencyId=&amp;fromUtc=&amp;toUtc=&amp;pageNumber=&amp;pageSize=
/// One currency per statement (default: the base currency). Lines are ordered by date; the running balance
/// continues across pages. Search and sorting are not used (a statement is always chronological).
/// </summary>
public sealed class CustomerStatementQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = Array.Empty<string>();

    public long? CurrencyId { get; set; }

    /// <summary>Inclusive. Default: everything (opening balance = 0).</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Exclusive. Default: now.</summary>
    public DateTime? ToUtc { get; set; }
}

public sealed class StatementLineDto
{
    public long TransactionId { get; init; }
    public DateTime TransactionDateUtc { get; init; }
    public CustomerAccountTransactionType TransactionType { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public long DocumentId { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public decimal Debit { get; init; }
    public decimal Credit { get; init; }
    /// <summary>Running balance after this line (positive = the customer owes us).</summary>
    public decimal Balance { get; init; }
}

public sealed class CustomerStatementDto
{
    public long CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public decimal CreditLimit { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime? FromUtc { get; init; }
    public DateTime ToUtc { get; init; }

    /// <summary>
    /// Balance before FromUtc. Includes the customer's opening balance (Customer.OpeningBalance) when the
    /// statement is in the base currency.
    /// </summary>
    public decimal OpeningBalance { get; init; }

    /// <summary>Debits / credits of the whole period (all pages).</summary>
    public decimal TotalDebit { get; init; }
    public decimal TotalCredit { get; init; }
    public decimal ClosingBalance { get; init; }

    /// <summary>Open invoices (balance &gt; 0) right now.</summary>
    public int OpenInvoiceCount { get; init; }
    public decimal OpenInvoicesBalance { get; init; }
    public decimal OverdueBalance { get; init; }

    /// <summary>Money received but not allocated + credit notes not applied to an invoice.</summary>
    public decimal UnappliedCredits { get; init; }

    /// <summary>When a branch user asks: the statement only contains that branch's documents.</summary>
    public long? BranchId { get; init; }

    public PagedResult<StatementLineDto> Lines { get; init; } = null!;
}

// ------------------------------------------------------------------ aging report

/// <summary>
/// GET /api/v1/reports/customer-aging. Ages the open invoice balances by days past the due date, as of AsOfUtc
/// (default: now), for one currency (default: the base currency).
/// Search: customer code / name / phone. SortBy: totalDue (default, highest first), customerName, overdue,
/// days90Plus.
/// </summary>
public sealed class CustomerAgingQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "totalDue", "customerName", "overdue", "days90Plus" };

    public DateTime? AsOfUtc { get; set; }

    public long? CurrencyId { get; set; }

    public long? BranchId { get; set; }

    public long? CustomerId { get; set; }

    /// <summary>Default true: hide customers whose total due and unapplied credits are both 0.</summary>
    public bool OnlyWithBalance { get; set; } = true;
}

public sealed class CustomerAgingRowDto
{
    public long CustomerId { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public decimal CreditLimit { get; init; }

    /// <summary>Not yet due.</summary>
    public decimal Current { get; init; }
    public decimal Days1To30 { get; init; }
    public decimal Days31To60 { get; init; }
    public decimal Days61To90 { get; init; }
    public decimal Days90Plus { get; init; }

    /// <summary>Sum of the buckets past due.</summary>
    public decimal Overdue { get; init; }

    public decimal TotalDue { get; init; }

    /// <summary>Unallocated payments + unapplied credit notes (reduce what the customer owes).</summary>
    public decimal UnappliedCredits { get; init; }

    /// <summary>TotalDue - UnappliedCredits.</summary>
    public decimal NetBalance { get; init; }

    public int OpenInvoiceCount { get; init; }

    /// <summary>Days past due of the oldest unpaid invoice (0 = nothing overdue).</summary>
    public int OldestDaysPastDue { get; init; }
}

public sealed class CustomerAgingReportDto
{
    public DateTime AsOfUtc { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public long? BranchId { get; init; }

    /// <summary>Totals of ALL customers of the report (not only the page).</summary>
    public CustomerAgingRowDto Totals { get; init; } = null!;

    public PagedResult<CustomerAgingRowDto> Customers { get; init; } = null!;
}

/// <summary>Step 9: what a customer owes, per currency (debit - credit of the sub-ledger, + opening balance).</summary>
public sealed class CustomerBalanceDto
{
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>Positive = the customer owes us; negative = paid in advance (credit).</summary>
    public decimal Balance { get; init; }
}
