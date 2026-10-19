using ShippingERP.Domain.Common;
using ShippingERP.Domain.Entities.MasterData;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Entities.Accounting;

/// <summary>
/// A value of an analytical dimension (بُعد تحليلي): a cost center (مركز تكلفة) such as "العمليات" or "الأسطول",
/// a project, a vehicle, a department. The branch is a built-in dimension of every line (JournalEntryLine.BranchId).
/// </summary>
public class AccountingDimension : SoftDeletableEntity
{
    public DimensionType DimensionType { get; set; } = DimensionType.CostCenter;

    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    /// <summary>Optional: the branch the value belongs to (null = company wide).</summary>
    public long? BranchId { get; set; }

    public Branch? Branch { get; set; }

    public long? ParentId { get; set; }

    public AccountingDimension? Parent { get; set; }

    public string? Description { get; set; }
}
