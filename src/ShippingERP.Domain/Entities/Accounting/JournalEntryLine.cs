using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// Journal line (سطر القيد): exactly one of Debit / Credit is greater than zero. Amounts are in the entry
/// currency; DebitBase / CreditBase are the same amounts in the base currency (all reports use them).
///
/// Lines are immutable once the entry leaves Draft / Rejected (JournalIntegrityInterceptor + SQL trigger).
/// Branch and the optional sub-ledger references (customer, supplier, driver, employee, cashbox, bank account,
/// expense category) let the reports reconcile the general ledger with the operational sub-ledgers.
/// </summary>
public class JournalEntryLine : BaseEntity
{
    public long JournalEntryId { get; set; }

    public JournalEntry JournalEntry { get; set; } = null!;

    public int LineNumber { get; set; }

    public long AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal DebitBase { get; set; }

    public decimal CreditBase { get; set; }

    /// <summary>Branch dimension (always set).</summary>
    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    /// <summary>The posting role that produced the line (automatic entries), e.g. "DriverCustody".</summary>
    public string? AccountRole { get; set; }

    public long? CustomerId { get; set; }

    public long? SupplierId { get; set; }

    /// <summary>DriverProfile id.</summary>
    public long? DriverId { get; set; }

    public long? EmployeeId { get; set; }

    public long? CashboxId { get; set; }

    public long? BankAccountId { get; set; }

    public long? ExpenseCategoryId { get; set; }

    /// <summary>Cost center, project, vehicle... (one per dimension type).</summary>
    public ICollection<JournalEntryDimension> Dimensions { get; set; } = new List<JournalEntryDimension>();
}
