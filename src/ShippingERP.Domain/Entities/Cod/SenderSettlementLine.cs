using ShippingERP.Domain.Common;

namespace ShippingERP.Domain.Entities.Cod;

/// <summary>
/// One COD shipment in a sender settlement.
///
/// Amount = what this settlement pays for that COD (all or part of its remaining net amount; a negative remaining
/// amount, i.e. the charges are higher than the collected money, is always taken in full and reduces the total).
/// The deductions and compensation of a COD are recognized ONCE, on its first settlement line; later lines of a
/// partially settled COD carry zeros. GrossReleased = Amount + recognized deductions - recognized compensation:
/// the part of the money held for the sender that this line releases.
///
/// IsOpen = the settlement is Draft or Approved. A COD can be in only one open line at a time (filtered unique
/// index), so the same money can never be promised to the sender twice.
/// </summary>
public class SenderSettlementLine : BaseEntity
{
    public long SenderSettlementId { get; set; }

    public SenderSettlement SenderSettlement { get; set; } = null!;

    public long CODTransactionId { get; set; }

    public CODTransaction CODTransaction { get; set; } = null!;

    public long ShipmentId { get; set; }

    /// <summary>The COD collected amount (information).</summary>
    public decimal CollectedAmount { get; set; }

    /// <summary>The remaining net amount of the COD when the line was created.</summary>
    public decimal RemainingBefore { get; set; }

    public decimal ShippingCharge { get; set; }

    public decimal CodCommission { get; set; }

    public decimal Tax { get; set; }

    public decimal OtherFees { get; set; }

    public decimal Compensation { get; set; }

    /// <summary>True when this line recognizes the COD's deductions (its first settlement).</summary>
    public bool RecognizesDeductions { get; set; }

    public decimal Amount { get; set; }

    public decimal GrossReleased { get; set; }

    public bool IsOpen { get; set; } = true;
}
