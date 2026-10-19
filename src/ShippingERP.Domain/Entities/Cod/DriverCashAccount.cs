using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.HR;
using ShippingERP.Domain.Entities.MasterData;

namespace ShippingERP.Domain.Entities.Cod;

/// <summary>
/// The cash a driver holds for the company (عهدة السائق), one account per driver and currency, created on the
/// first movement. Balance = sum(Debit - Credit) of its DriverCashMovements, kept here for fast reads and
/// protected by RowVersion: two operations changing the same balance at the same moment cannot both succeed.
/// A positive balance is money the driver must hand over; a negative one is money the company owes him.
/// </summary>
public class DriverCashAccount : BaseEntity
{
    public long DriverId { get; set; }

    public DriverProfile Driver { get; set; } = null!;

    public long CurrencyId { get; set; }

    public Currency Currency { get; set; } = null!;

    public decimal Balance { get; set; }

    public DateTime? LastMovementAtUtc { get; set; }

    public DateTime? LastHandoverAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<DriverCashMovement> Movements { get; set; } = new List<DriverCashMovement>();
}
