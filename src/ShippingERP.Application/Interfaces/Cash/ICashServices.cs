using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;

namespace ShippingERP.Application.Interfaces.Cash;

// Cash management (step 6). Branch users only see cashboxes, vouchers and closings of their branch, and transfers
// from or to it. Every cashbox movement is an append-only CashboxTransaction written in the same transaction as
// its document, together with the audit log and the outbox event; the cashbox balance can never go below zero
// and a closed day cannot receive new movements.

public interface ICashboxService
{
    Task<PagedResult<CashboxDto>> GetPagedAsync(CashboxListQuery query, CancellationToken cancellationToken);

    Task<CashboxDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<CashboxDto> CreateAsync(CreateCashboxRequest request, CancellationToken cancellationToken);

    Task<CashboxDto> UpdateAsync(long id, UpdateCashboxRequest request, CancellationToken cancellationToken);

    Task<PagedResult<CashboxTransactionDto>> GetTransactionsAsync(long id, CashboxTransactionQuery query, CancellationToken cancellationToken);

    Task<CashboxDailyClosingDto> CloseDayAsync(long id, CloseCashboxDayRequest request, CancellationToken cancellationToken);

    Task<PagedResult<CashboxDailyClosingDto>> GetClosingsAsync(CashboxClosingQuery query, CancellationToken cancellationToken);

    /// <summary>Posts the counted difference (another user than the one who closed).</summary>
    Task<CashboxDailyClosingDto> ApproveClosingAsync(long closingId, DecideCashboxClosingRequest request, CancellationToken cancellationToken);

    /// <summary>Rejects the count: the day is open again and must be counted again.</summary>
    Task<CashboxDailyClosingDto> RejectClosingAsync(long closingId, DecideCashboxClosingRequest request, CancellationToken cancellationToken);
}

public interface IBankAccountService
{
    Task<PagedResult<BankAccountDto>> GetPagedAsync(BankAccountListQuery query, CancellationToken cancellationToken);

    Task<BankAccountDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<BankAccountDto> CreateAsync(SaveBankAccountRequest request, CancellationToken cancellationToken);

    Task<BankAccountDto> UpdateAsync(long id, SaveBankAccountRequest request, CancellationToken cancellationToken);

    /// <summary>Step 7: soft delete; refused when the account has transfers, settlements, ledger lines or statement lines.</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken);
}

public interface ICashVoucherService
{
    Task<PagedResult<CashReceiptVoucherDto>> GetReceiptsAsync(CashVoucherListQuery query, CancellationToken cancellationToken);

    Task<CashReceiptVoucherDto> GetReceiptAsync(long id, CancellationToken cancellationToken);

    Task<CashVoucherCreationResult<CashReceiptVoucherDto>> CreateReceiptAsync(CreateCashReceiptRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a receipt voucher (reversing cashbox transaction). A driver handover gives the money back to the
    /// driver and the CODs back to "collected by driver"; a counter COD collection sends the COD back to pending.
    /// </summary>
    Task<CashReceiptVoucherDto> CancelReceiptAsync(long id, CancelCashVoucherRequest request, CancellationToken cancellationToken);

    Task<FileDownload> GetReceiptPdfAsync(long id, CancellationToken cancellationToken);

    Task<PagedResult<CashPaymentVoucherDto>> GetPaymentsAsync(CashVoucherListQuery query, CancellationToken cancellationToken);

    Task<CashPaymentVoucherDto> GetPaymentAsync(long id, CancellationToken cancellationToken);

    Task<CashVoucherCreationResult<CashPaymentVoucherDto>> CreatePaymentAsync(CreateCashPaymentRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Cancels a payment voucher (the money returns to the cashbox). Settlement vouchers: reverse the settlement instead.</summary>
    Task<CashPaymentVoucherDto> CancelPaymentAsync(long id, CancelCashVoucherRequest request, CancellationToken cancellationToken);

    Task<FileDownload> GetPaymentPdfAsync(long id, CancellationToken cancellationToken);
}

public interface ICashTransferService
{
    Task<PagedResult<CashTransferDto>> GetPagedAsync(CashTransferListQuery query, CancellationToken cancellationToken);

    Task<CashTransferDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<CashTransferDto> CreateAsync(CreateCashTransferRequest request, CancellationToken cancellationToken);

    Task<CashTransferDto> ApproveAsync(long id, ApproveCashTransferRequest request, CancellationToken cancellationToken);

    Task<CashTransferDto> RejectAsync(long id, CashTransferReasonRequest request, CancellationToken cancellationToken);

    Task<CashTransferDto> CancelAsync(long id, CashTransferReasonRequest request, CancellationToken cancellationToken);

    /// <summary>The destination branch confirms an inter-branch transfer that is in transit.</summary>
    Task<CashTransferDto> ReceiveAsync(long id, CancellationToken cancellationToken);
}

public interface ICashReportService
{
    Task<DailyCashMovementReportDto> GetDailyCashMovementAsync(DailyCashMovementQuery query, CancellationToken cancellationToken);
}
