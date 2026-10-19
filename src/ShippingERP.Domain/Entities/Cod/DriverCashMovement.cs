using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Cod;

/// <summary>
/// One entry of the driver cash sub-ledger (حركة عهدة السائق). Append-only; written in the same transaction as
/// the collection, handover or approved adjustment that caused it.
///   Debit  = the driver holds more (COD collected, excess recorded, handover reversed)
///   Credit = the driver holds less (handover, shortage written off, collection reversed)
/// </summary>
public class DriverCashMovement : BaseEntity, IAppendOnlyEntity
{
    public long DriverCashAccountId { get; set; }

    public DriverCashAccount DriverCashAccount { get; set; } = null!;

    /// <summary>Copied from the account for reports.</summary>
    public long DriverId { get; set; }

    public long CurrencyId { get; set; }

    public DriverCashMovementType MovementType { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    /// <summary>The account balance right after this movement.</summary>
    public decimal BalanceAfter { get; set; }

    public DateTime MovementDateUtc { get; set; }

    public long? CODTransactionId { get; set; }

    /// <summary>"CODTransaction", "CashReceiptVoucher" or "CODAdjustment".</summary>
    public string DocumentType { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
