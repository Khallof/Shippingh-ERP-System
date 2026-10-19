using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cash;

/// <summary>
/// One movement of a cashbox (حركة صندوق). Append-only: a mistake is corrected by a Reversal row, never by an
/// edit. BalanceAfter gives the running balance for the cashbox statement and the daily closing.
/// BusinessDate is the local business day (Cash:BusinessTimeZoneId, default Asia/Damascus) at midnight, stored
/// as a date; TransactionDateUtc is the exact UTC time.
/// </summary>
public class CashboxTransaction : BaseEntity, IAppendOnlyEntity
{
    public long CashboxId { get; set; }

    public Cashbox Cashbox { get; set; } = null!;

    /// <summary>Copied from the cashbox for reports.</summary>
    public long BranchId { get; set; }

    public long CurrencyId { get; set; }

    public CashboxTransactionType TransactionType { get; set; }

    public decimal AmountIn { get; set; }

    public decimal AmountOut { get; set; }

    public decimal BalanceAfter { get; set; }

    public DateTime TransactionDateUtc { get; set; }

    public DateTime BusinessDate { get; set; }

    /// <summary>"CashReceiptVoucher", "CashPaymentVoucher", "CashTransfer", "SenderSettlement", "CashboxDailyClosing", "Cashbox".</summary>
    public string DocumentType { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Who paid / was paid (driver, customer, supplier...).</summary>
    public string? CounterpartyName { get; set; }

    /// <summary>For Reversal rows: the transaction that is cancelled.</summary>
    public long? ReversedTransactionId { get; set; }
}
