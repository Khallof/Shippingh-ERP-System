using ShippingERP.Application.DTOs.Common;
using ShippingERP.Domain.Enums;

namespace ShippingERP.Application.DTOs.Cash;

// ============================================================ cashboxes

/// <summary>GET /api/v1/cashboxes. Search: code / name. SortBy: code (default), arabicName, currentBalance.</summary>
public sealed class CashboxListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "code", "arabicName", "currentBalance" };

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    public bool? IsActive { get; set; }
}

/// <summary>Body of POST /api/v1/cashboxes. Branch and currency cannot change later.</summary>
public sealed class CreateCashboxRequest
{
    /// <summary>Unique, e.g. "DAM-SYP-01" (upper case letters, digits, '-' and '_').</summary>
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public long BranchId { get; set; }

    public long CurrencyId { get; set; }

    public long? CustodianUserId { get; set; }

    /// <summary>Cash already in the box (posted as an opening-balance transaction).</summary>
    public decimal OpeningBalance { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Body of PUT /api/v1/cashboxes/{id}. Deactivation needs a zero balance.</summary>
public sealed class UpdateCashboxRequest
{
    public string ArabicName { get; set; } = string.Empty;

    public string? EnglishName { get; set; }

    public long? CustodianUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}

public sealed class CashboxDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string? EnglishName { get; init; }
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public int CurrencyDecimals { get; init; }
    public long? CustodianUserId { get; init; }
    public string? CustodianName { get; init; }
    public decimal CurrentBalance { get; init; }
    public DateTime? LastTransactionAtUtc { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public long? UpdatedByUserId { get; init; }
}

/// <summary>
/// GET /api/v1/cashboxes/{id}/transactions (newest first). Search: document number, description, counterparty.
/// FromDate / ToDate are local business dates (inclusive).
/// </summary>
public sealed class CashboxTransactionQuery : PagedQuery
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public CashboxTransactionType? TransactionType { get; set; }
}

public sealed class CashboxTransactionDto
{
    public long Id { get; init; }
    public CashboxTransactionType TransactionType { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public decimal AmountIn { get; init; }
    public decimal AmountOut { get; init; }
    public decimal BalanceAfter { get; init; }
    public DateTime TransactionDateUtc { get; init; }
    public DateTime BusinessDate { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public long DocumentId { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? CounterpartyName { get; init; }
    public long? ReversedTransactionId { get; init; }
    public long? CreatedByUserId { get; init; }
}

// ============================================================ daily closing

/// <summary>
/// Body of POST /api/v1/cashboxes/{id}/daily-closings. BusinessDate = the local day being closed (default today).
/// A difference between CountedBalance and the expected balance needs DiscrepancyReason and a second user's approval.
/// </summary>
public sealed class CloseCashboxDayRequest
{
    public DateTime? BusinessDate { get; set; }

    public decimal CountedBalance { get; set; }

    public string? DiscrepancyReason { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Approve (notes optional) or reject (notes required) a closing difference.</summary>
public sealed class DecideCashboxClosingRequest
{
    public string? Notes { get; set; }
}

/// <summary>
/// GET /api/v1/cashboxes/daily-closings (all cashboxes in scope) and /cashboxes/{id}/daily-closings.
/// HasDiscrepancy = true gives the cash discrepancy report. Newest day first.
/// </summary>
public sealed class CashboxClosingQuery : PagedQuery
{
    public long? CashboxId { get; set; }

    public long? BranchId { get; set; }

    public CashboxClosingStatus? Status { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public bool? HasDiscrepancy { get; set; }
}

public sealed class CashboxDailyClosingDto
{
    public long Id { get; init; }
    public long CashboxId { get; init; }
    public string CashboxCode { get; init; } = string.Empty;
    public string CashboxName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime BusinessDate { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal ExpectedBalance { get; init; }
    public decimal CountedBalance { get; init; }
    public decimal Discrepancy { get; init; }
    public CashboxClosingStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string? DiscrepancyReason { get; init; }
    public string? Notes { get; init; }
    public long? ClosedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? DecidedByUserId { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? DecisionNotes { get; init; }
    public long? AdjustmentTransactionId { get; init; }
}

// ============================================================ bank accounts

/// <summary>GET /api/v1/bank-accounts. Search: code, name, bank name, account number.</summary>
public sealed class BankAccountListQuery : PagedQuery
{
    public long? CurrencyId { get; set; }

    public long? BranchId { get; set; }

    public bool? IsActive { get; set; }
}

/// <summary>Body of POST / PUT /api/v1/bank-accounts. The currency cannot change once the account is used.</summary>
public sealed class SaveBankAccountRequest
{
    public string Code { get; set; } = string.Empty;

    public string ArabicName { get; set; } = string.Empty;

    public string BankName { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string? Iban { get; set; }

    public long CurrencyId { get; set; }

    public long? BranchId { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    /// <summary>Step 7: the general ledger account (postable asset account). Null = the "Bank" posting rule account.</summary>
    public long? AccountId { get; set; }
}

public sealed class BankAccountDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string ArabicName { get; init; } = string.Empty;
    public string BankName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string? Iban { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public long? BranchId { get; init; }
    public string? BranchName { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? AccountId { get; init; }
    public string? AccountCode { get; init; }
    public string? AccountName { get; init; }
}

// ============================================================ vouchers

/// <summary>
/// Body of POST /api/v1/cash-receipts (send an "Idempotency-Key" header). Types: CustomerPayment (deposits the
/// cash of an existing step-5 cash payment: CustomerPaymentId, Amount = its amount), OtherIncome, Other.
/// DriverHandover / CounterCodCollection vouchers are created by the COD endpoints only.
/// </summary>
public sealed class CreateCashReceiptRequest
{
    public long CashboxId { get; set; }

    public CashReceiptType ReceiptType { get; set; }

    public decimal Amount { get; set; }

    public string ReceivedFrom { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ReferenceNumber { get; set; }

    public long? CustomerId { get; set; }

    public long? CustomerPaymentId { get; set; }
}

/// <summary>
/// Body of POST /api/v1/cash-payments (send an "Idempotency-Key" header). Types and their link:
/// Expense (ExpenseCategoryId), SupplierPayment (SupplierId), EmployeeAdvance (EmployeeId), CustomerRefund
/// (CustomerId), Other. SenderSettlement vouchers are created by POST /cod/settlements/{id}/pay only.
/// </summary>
public sealed class CreateCashPaymentRequest
{
    public long CashboxId { get; set; }

    public CashPaymentType PaymentType { get; set; }

    public decimal Amount { get; set; }

    public string PaidTo { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? ReferenceNumber { get; set; }

    public long? ExpenseCategoryId { get; set; }

    public long? SupplierId { get; set; }

    public long? EmployeeId { get; set; }

    public long? CustomerId { get; set; }
}

public sealed class CancelCashVoucherRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/v1/cash-receipts and /cash-payments. Search: voucher number, name, description, reference.
/// SortBy: voucherDateUtc (default, newest first), voucherNumber, amount.
/// </summary>
public sealed class CashVoucherListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "voucherDateUtc", "voucherNumber", "amount" };

    public long? CashboxId { get; set; }

    public long? BranchId { get; set; }

    public CashVoucherStatus? Status { get; set; }

    /// <summary>Receipts: a CashReceiptType name; payments: a CashPaymentType name (e.g. "Expense").</summary>
    public string? Type { get; set; }

    public long? DriverId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

public sealed class HandoverCodLineDto
{
    public long CodTransactionId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string SenderName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public sealed class CashReceiptVoucherDto
{
    public long Id { get; init; }
    public string VoucherNumber { get; init; } = string.Empty;
    public long CashboxId { get; init; }
    public string CashboxCode { get; init; } = string.Empty;
    public string CashboxName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string CurrencyName { get; init; } = string.Empty;
    public int CurrencyDecimals { get; init; }
    public CashReceiptType ReceiptType { get; init; }
    public string ReceiptTypeName { get; init; } = string.Empty;
    public CashVoucherStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime VoucherDateUtc { get; init; }
    public decimal Amount { get; init; }
    public string AmountInWords { get; init; } = string.Empty;
    public string ReceivedFrom { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
    public long? DriverId { get; init; }
    public string? DriverName { get; init; }
    public long? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public long? CustomerPaymentId { get; init; }
    public string? CustomerPaymentNumber { get; init; }
    public decimal? ExpectedAmount { get; init; }
    public decimal ShortageAmount { get; init; }
    public decimal ExcessAmount { get; init; }
    /// <summary>Driver handover: the driver's balance right after this handover.</summary>
    public decimal? DriverBalanceAfter { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public long? CancelledByUserId { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
    public IReadOnlyList<HandoverCodLineDto> CodLines { get; init; } = Array.Empty<HandoverCodLineDto>();
}

public sealed class CashPaymentVoucherDto
{
    public long Id { get; init; }
    public string VoucherNumber { get; init; } = string.Empty;
    public long CashboxId { get; init; }
    public string CashboxCode { get; init; } = string.Empty;
    public string CashboxName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string CurrencyName { get; init; } = string.Empty;
    public int CurrencyDecimals { get; init; }
    public CashPaymentType PaymentType { get; init; }
    public string PaymentTypeName { get; init; } = string.Empty;
    public CashVoucherStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTime VoucherDateUtc { get; init; }
    public decimal Amount { get; init; }
    public string AmountInWords { get; init; } = string.Empty;
    public string PaidTo { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
    public long? ExpenseCategoryId { get; init; }
    public string? ExpenseCategoryName { get; init; }
    public long? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public long? EmployeeId { get; init; }
    public string? EmployeeName { get; init; }
    public long? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public long? SenderSettlementId { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public long? CancelledByUserId { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? CreatedByUserId { get; init; }
}

/// <summary>Result of a voucher POST: IsReplay = true when the Idempotency-Key was already used.</summary>
public sealed class CashVoucherCreationResult<T>
{
    public T Voucher { get; init; } = default!;

    public bool IsReplay { get; init; }
}

// ============================================================ transfers

/// <summary>
/// Body of POST /api/v1/cash-transfers: ToCashboxId (same currency; same or another branch) or ToBankAccountId
/// (same currency; BankReference required now or at approval). Nothing moves before approval.
/// </summary>
public sealed class CreateCashTransferRequest
{
    public long FromCashboxId { get; set; }

    public long? ToCashboxId { get; set; }

    public long? ToBankAccountId { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? BankReference { get; set; }
}

public sealed class ApproveCashTransferRequest
{
    /// <summary>Bank deposit reference (bank transfers, when not given at creation).</summary>
    public string? BankReference { get; set; }

    public string? Notes { get; set; }
}

public sealed class CashTransferReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/v1/cash-transfers (transfers from or to the caller's branch). Search: transfer number, reason.
/// SortBy: createdAtUtc (default, newest first), amount, transferNumber.
/// </summary>
public sealed class CashTransferListQuery : PagedQuery
{
    public static readonly string[] AllowedSortFields = { "createdAtUtc", "amount", "transferNumber" };

    public CashTransferStatus? Status { get; set; }

    public CashTransferType? TransferType { get; set; }

    public long? CashboxId { get; set; }

    public long? BranchId { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }
}

public sealed class CashTransferDto
{
    public long Id { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public CashTransferType TransferType { get; init; }
    public string TransferTypeName { get; init; } = string.Empty;
    public CashTransferStatus Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public long FromCashboxId { get; init; }
    public string FromCashboxName { get; init; } = string.Empty;
    public long FromBranchId { get; init; }
    public string FromBranchName { get; init; } = string.Empty;
    public long? ToCashboxId { get; init; }
    public string? ToCashboxName { get; init; }
    public long? ToBranchId { get; init; }
    public string? ToBranchName { get; init; }
    public long? ToBankAccountId { get; init; }
    public string? ToBankAccountName { get; init; }
    public long CurrencyId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? BankReference { get; init; }
    public long? RequestedByUserId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public long? DecidedByUserId { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? DecisionNotes { get; init; }
    public DateTime? SentAtUtc { get; init; }
    public DateTime? ReceivedAtUtc { get; init; }
    public long? ReceivedByUserId { get; init; }
}

// ============================================================ reports

/// <summary>
/// GET /api/v1/reports/daily-cash-movement: one row per cashbox and local business day (max 62 days):
/// opening balance, money in / out by kind, closing balance and the closing status.
/// </summary>
public sealed class DailyCashMovementQuery
{
    /// <summary>Default: 6 days before ToDate.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Default: today.</summary>
    public DateTime? ToDate { get; set; }

    public long? CashboxId { get; set; }

    public long? BranchId { get; set; }

    public long? CurrencyId { get; set; }

    /// <summary>Default false: days without any movement are skipped.</summary>
    public bool IncludeEmptyDays { get; set; }
}

public sealed class DailyCashMovementRowDto
{
    public long CashboxId { get; init; }
    public string CashboxCode { get; init; } = string.Empty;
    public string CashboxName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime BusinessDate { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal DriverHandovers { get; init; }
    public decimal CounterCollections { get; init; }
    public decimal Receipts { get; init; }
    public decimal TransfersIn { get; init; }
    public decimal OtherIn { get; init; }
    public decimal TotalIn { get; init; }
    public decimal SettlementPayments { get; init; }
    public decimal Payments { get; init; }
    public decimal TransfersOut { get; init; }
    public decimal OtherOut { get; init; }
    public decimal TotalOut { get; init; }
    public decimal ClosingBalance { get; init; }
    public int TransactionCount { get; init; }
    public CashboxClosingStatus? ClosingStatus { get; init; }
    public decimal? CountedBalance { get; init; }
    public decimal? Discrepancy { get; init; }
}

public sealed class DailyCashCurrencyTotalDto
{
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal Net { get; init; }
}

public sealed class DailyCashMovementReportDto
{
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public string TimeZoneId { get; init; } = string.Empty;
    public IReadOnlyList<DailyCashMovementRowDto> Rows { get; init; } = Array.Empty<DailyCashMovementRowDto>();
    public IReadOnlyList<DailyCashCurrencyTotalDto> Totals { get; init; } = Array.Empty<DailyCashCurrencyTotalDto>();
}
