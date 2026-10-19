using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Cod;

public sealed class SettlementLineRequest
{
    public long CodTransactionId { get; set; }

    /// <summary>Null = the whole remaining net amount. Less = partial settlement of this COD.</summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// Body of POST /api/v1/cod/settlements. Choose the COD shipments with Lines (partial amounts allowed) or
/// CodTransactionIds (whole remaining amounts). Without both, every ready COD of the sender in the currency is
/// taken. Only ReadyForSenderSettlement / PartiallySettled CODs not reserved by another open settlement qualify.
/// </summary>
public sealed class CreateSenderSettlementRequest
{
    public long SenderCustomerId { get; set; }

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }

    /// <summary>The paying branch. Default: the caller's branch, else the sender's branch.</summary>
    public long? BranchId { get; set; }

    public List<long> CodTransactionIds { get; set; } = new();

    public List<SettlementLineRequest> Lines { get; set; } = new();

    public string? Notes { get; set; }
}

public sealed class ApproveSenderSettlementRequest
{
    public string? Notes { get; set; }
}

/// <summary>
/// Body of POST /api/v1/cod/settlements/{id}/pay.
/// Cash method: CashboxId (same currency, enough balance) - a payment voucher is created.
/// Bank transfer method: BankAccountId (same currency) and ReferenceNumber.
/// </summary>
public sealed class PaySenderSettlementRequest
{
    public long PaymentMethodId { get; set; }

    public long? CashboxId { get; set; }

    public long? BankAccountId { get; set; }

    public string? ReferenceNumber { get; set; }

    /// <summary>Default: now. Cannot be in the future.</summary>
    public DateTime? PaidAtUtc { get; set; }
}

/// <summary>Cancel (Draft / Approved) or reverse (Paid) a settlement.</summary>
public sealed class SettlementReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/v1/cod/settlements. Search: settlement number, sender code / name.
/// SortBy: settlementDateUtc (default, newest first), settlementNumber, netPayable.
/// </summary>
public sealed class SenderSettlementListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "settlementDateUtc", "settlementNumber", "netPayable" };

    public SettlementStatus? Status { get; set; }

    public long? SenderCustomerId { get; set; }

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

/// <summary>GET /api/v1/cod/settlements/candidates: what can be settled now for a sender.</summary>
public sealed class SettlementCandidatesQuery
{
    public long SenderCustomerId { get; set; }

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }
}

public sealed class SettlementCandidateDto
{
    public long CodTransactionId { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public CODStatus Status { get; init; }
    public decimal CollectedAmount { get; init; }
    public decimal ShippingChargeDeduction { get; init; }
    public decimal CodCommissionDeduction { get; init; }
    public decimal TaxDeduction { get; init; }
    public decimal OtherFeesAmount { get; init; }
    public decimal CompensationAmount { get; init; }
    public decimal NetPayableAmount { get; init; }
    public decimal SettledAmount { get; init; }
    public decimal RemainingPayable { get; init; }
    public DateTime? CollectedAtUtc { get; init; }
}

public sealed class SenderSettlementLineDto
{
    public long Id { get; init; }
    public long CodTransactionId { get; init; }
    public long ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    public DateTime? DeliveredAtUtc { get; init; }
    public decimal CollectedAmount { get; init; }
    public decimal RemainingBefore { get; init; }
    public decimal ShippingCharge { get; init; }
    public decimal CodCommission { get; init; }
    public decimal Tax { get; init; }
    public decimal OtherFees { get; init; }
    public decimal Compensation { get; init; }
    public bool RecognizesDeductions { get; init; }
    public decimal Amount { get; init; }
    public decimal GrossReleased { get; init; }
}

public sealed class SenderSettlementListItemDto
{
    public long Id { get; init; }
    public string SettlementNumber { get; init; } = string.Empty;
    public long SenderCustomerId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public SettlementStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime SettlementDateUtc { get; init; }
    public int LineCount { get; init; }
    public decimal TotalGrossReleased { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetPayable { get; init; }
    public DateTime? PaidAtUtc { get; init; }
}

public sealed class SenderSettlementDto
{
    public long Id { get; init; }
    public string SettlementNumber { get; init; } = string.Empty;
    public long SenderCustomerId { get; init; }
    public string SenderCode { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public string? SenderPhone { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string CurrencyName { get; init; } = string.Empty;
    public int CurrencyDecimals { get; init; }
    public SettlementStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime SettlementDateUtc { get; init; }

    public decimal TotalGrossReleased { get; init; }
    public decimal TotalShippingCharges { get; init; }
    public decimal TotalCodCommission { get; init; }
    public decimal TotalTax { get; init; }
    public decimal TotalOtherFees { get; init; }
    public decimal TotalCompensation { get; init; }
    public decimal NetPayable { get; init; }
    public string? Notes { get; init; }

    public long? CreatedByUserId { get; init; }
    public long? ApprovedByUserId { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }

    public long? PaymentMethodId { get; init; }
    public string? PaymentMethodName { get; init; }
    public long? CashboxId { get; init; }
    public string? CashboxName { get; init; }
    public long? BankAccountId { get; init; }
    public string? BankAccountName { get; init; }
    public string? PaymentReference { get; init; }
    public long? CashPaymentVoucherId { get; init; }
    public string? CashPaymentVoucherNumber { get; init; }
    public DateTime? PaidAtUtc { get; init; }
    public long? PaidByUserId { get; init; }

    public DateTime? CancelledAtUtc { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime? ReversedAtUtc { get; init; }
    public string? ReversalReason { get; init; }

    public IReadOnlyList<SenderSettlementLineDto> Lines { get; init; } = Array.Empty<SenderSettlementLineDto>();
}

/// <summary>GET /api/v1/reports/sender-settlements: settlements of a period with totals.</summary>
public sealed class SenderSettlementReportQuery
{
    /// <summary>Default: the start of the local business day 29 days before the end (30 days in all).</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>Exclusive. Default: the end of today (local business day).</summary>
    public DateTime? ToUtc { get; set; }

    /// <summary>Default: the base currency.</summary>
    public long? CurrencyId { get; set; }

    public long? BranchId { get; set; }

    public long? SenderCustomerId { get; set; }

    /// <summary>Default: Paid.</summary>
    public SettlementStatus? Status { get; set; }
}

public sealed class SenderSettlementReportDto
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public SettlementStatus Status { get; init; }
    public IReadOnlyList<SenderSettlementListItemDto> Rows { get; init; } = Array.Empty<SenderSettlementListItemDto>();
    public int SettlementCount { get; init; }
    public int ShipmentCount { get; init; }
    public decimal TotalGrossReleased { get; init; }
    public decimal TotalShippingCharges { get; init; }
    public decimal TotalCodCommission { get; init; }
    public decimal TotalTax { get; init; }
    public decimal TotalOtherFees { get; init; }
    public decimal TotalCompensation { get; init; }
    public decimal TotalNetPaid { get; init; }
}
