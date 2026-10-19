using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.Customers;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Billing;

/// <summary>
/// One entry of the customer sub-ledger (دفتر حساب العميل): the audit trail of everything that changed what a
/// customer owes. Append-only; written in the same transaction as the invoice, payment or credit note.
///
///   Debit  = the customer owes more (invoice issued).
///   Credit = the customer owes less (payment, credit note, invoice cancelled).
///   Balance of a customer = sum(Debit - Credit) per currency (positive = the customer owes us).
///
/// The general ledger is NOT posted in this phase: the same events are written to the outbox for the accounting
/// module of the next phase.
/// </summary>
public class CustomerAccountTransaction : BaseEntity, IAppendOnlyEntity
{
    public long CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public DateTime TransactionDateUtc { get; set; }

    public CustomerAccountTransactionType TransactionType { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    /// <summary>"Invoice", "CustomerPayment" or "CreditNote".</summary>
    public string DocumentType { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
