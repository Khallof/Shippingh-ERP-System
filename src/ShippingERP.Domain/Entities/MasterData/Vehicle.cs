using ShippingERP.Domain.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.MasterData;

/// <summary>A company vehicle assigned to a branch. Plate numbers are unique.</summary>
public class Vehicle : SoftDeletableEntity
{
    /// <summary>Stored upper case without extra spaces, e.g. "DAM-123456".</summary>
    public string PlateNumber { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public VehicleType VehicleType { get; set; }

    /// <summary>Maximum load in kilograms.</summary>
    public decimal CapacityKg { get; set; }

    public long BranchId { get; set; }

    public Branch Branch { get; set; } = null!;

    public string? Notes { get; set; }
}
