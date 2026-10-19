using ShippingERP.Application.Common.Files;
using ShippingERP.Application.Common.Models;
using ShippingERP.Application.DTOs.Cash;
using ShippingERP.Application.DTOs.Cod;

namespace ShippingERP.Application.Interfaces.Cod;

// Cash on delivery (step 6).
// Scope: a driver-only user sees his own COD shipments and balance; branch users see COD of shipments that start,
// end or are in their branch; Branches.ViewAll sees everything. Records outside the scope are reported as 404.
// Every money change runs in ONE database transaction with its sub-ledger rows, history, audit log and outbox event.

public interface ICodService
{
    Task<PagedResult<CodTransactionListItemDto>> GetPagedAsync(CodTransactionListQuery query, CancellationToken cancellationToken);

    Task<CodTransactionDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<CodTransactionDto> GetByShipmentAsync(long shipmentId, CancellationToken cancellationToken);

    /// <summary>Records the money collected from the recipient of a delivered COD shipment (driver or counter).</summary>
    Task<CodTransactionDto> CollectAsync(CollectCodRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// A driver hands cash over to a cashbox: receipt voucher + cashbox in + driver balance out + CODs received.
    /// Same Idempotency-Key and body = the first voucher (IsReplay); same key, other body = 409.
    /// </summary>
    Task<CashVoucherCreationResult<CashReceiptVoucherDto>> HandoverAsync(DriverHandoverRequest request, string? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Calculates the deductions and net amount of received CODs and makes them ready for settlement.</summary>
    Task<IReadOnlyList<CodTransactionListItemDto>> MarkReadyAsync(MarkCodReadyRequest request, CancellationToken cancellationToken);

    Task<CodTransactionDto> OpenDisputeAsync(long id, OpenCodDisputeRequest request, CancellationToken cancellationToken);

    Task<CodTransactionDto> ResolveDisputeAsync(long id, ResolveCodDisputeRequest request, CancellationToken cancellationToken);
}

public interface ICodAdjustmentService
{
    Task<PagedResult<CodAdjustmentDto>> GetPagedAsync(CodAdjustmentListQuery query, CancellationToken cancellationToken);

    Task<CodAdjustmentDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Records the request (PendingApproval). Nothing changes until another user approves it.</summary>
    Task<CodAdjustmentDto> CreateAsync(CreateCodAdjustmentRequest request, CancellationToken cancellationToken);

    /// <summary>Applies the adjustment (driver balance, COD amounts, history, event) in one transaction.</summary>
    Task<CodAdjustmentDto> ApproveAsync(long id, DecideCodAdjustmentRequest request, CancellationToken cancellationToken);

    Task<CodAdjustmentDto> RejectAsync(long id, DecideCodAdjustmentRequest request, CancellationToken cancellationToken);
}

public interface ISenderSettlementService
{
    Task<PagedResult<SenderSettlementListItemDto>> GetPagedAsync(SenderSettlementListQuery query, CancellationToken cancellationToken);

    Task<SenderSettlementDto> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettlementCandidateDto>> GetCandidatesAsync(SettlementCandidatesQuery query, CancellationToken cancellationToken);

    Task<SenderSettlementDto> CreateAsync(CreateSenderSettlementRequest request, CancellationToken cancellationToken);

    /// <summary>Draft -> Approved, by a user other than the creator.</summary>
    Task<SenderSettlementDto> ApproveAsync(long id, ApproveSenderSettlementRequest request, CancellationToken cancellationToken);

    /// <summary>Approved -> Paid: cash (payment voucher + cashbox out) or bank transfer; CODs settled; event.</summary>
    Task<SenderSettlementDto> PayAsync(long id, PaySenderSettlementRequest request, CancellationToken cancellationToken);

    /// <summary>Draft / Approved -> Cancelled (the CODs are released).</summary>
    Task<SenderSettlementDto> CancelAsync(long id, SettlementReasonRequest request, CancellationToken cancellationToken);

    /// <summary>Paid -> Reversed: the payment is undone and the CODs are payable again.</summary>
    Task<SenderSettlementDto> ReverseAsync(long id, SettlementReasonRequest request, CancellationToken cancellationToken);

    /// <summary>Printable sender settlement statement (A4, Arabic).</summary>
    Task<FileDownload> GetPdfAsync(long id, CancellationToken cancellationToken);
}

public interface ICodReportService
{
    Task<PagedResult<DriverBalanceDto>> GetDriverBalancesAsync(DriverBalanceQuery query, CancellationToken cancellationToken);

    Task<PagedResult<DriverCashMovementDto>> GetDriverMovementsAsync(long driverId, DriverMovementQuery query, CancellationToken cancellationToken);

    Task<PagedResult<SenderBalanceDto>> GetSenderBalancesAsync(SenderBalanceQuery query, CancellationToken cancellationToken);

    Task<CodAgingReportDto> GetAgingAsync(CodAgingQuery query, CancellationToken cancellationToken);

    Task<CodDashboardDto> GetDashboardAsync(CodDashboardQuery query, CancellationToken cancellationToken);

    Task<DriverCollectionReportDto> GetDriverCollectionReportAsync(DriverCollectionReportQuery query, CancellationToken cancellationToken);

    Task<SenderSettlementReportDto> GetSenderSettlementReportAsync(SenderSettlementReportQuery query, CancellationToken cancellationToken);
}
