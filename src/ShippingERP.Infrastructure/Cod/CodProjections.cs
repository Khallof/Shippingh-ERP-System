using System.Linq.Expressions;
using ShippingERP.Application.DTOs.Cod;
using ShippingERP.Domain.Cod;
using ShippingERP.Domain.Entities.Cod;

namespace ShippingERP.Infrastructure.Cod;

/// <summary>EF Core projections of the COD records (translated to SQL; the Arabic names are added in memory).</summary>
public static class CodProjections
{
    public static readonly Expression<Func<CODTransaction, CodTransactionListItemDto>> ListItem = x => new CodTransactionListItemDto
    {
        Id = x.Id,
        ShipmentId = x.ShipmentId,
        ShipmentNumber = x.Shipment.ShipmentNumber,
        TrackingNumber = x.Shipment.TrackingNumber,
        ShipmentStatus = x.Shipment.Status,
        SenderCustomerId = x.SenderCustomerId,
        SenderName = x.SenderCustomer.ArabicName,
        DriverId = x.DriverId,
        DriverName = x.Driver != null ? x.Driver.Employee.ArabicName : null,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        Status = x.Status,
        ExpectedAmount = x.ExpectedAmount,
        CollectedAmount = x.CollectedAmount,
        NetPayableAmount = x.NetPayableAmount,
        SettledAmount = x.SettledAmount,
        RemainingPayable = x.NetPayableAmount - x.SettledAmount,
        CollectedAtUtc = x.CollectedAtUtc,
        ReceivedAtUtc = x.ReceivedAtUtc,
        CreatedAtUtc = x.CreatedAtUtc
    };

    public static readonly Expression<Func<CODTransaction, CodTransactionDto>> Detail = x => new CodTransactionDto
    {
        Id = x.Id,
        ShipmentId = x.ShipmentId,
        ShipmentNumber = x.Shipment.ShipmentNumber,
        TrackingNumber = x.Shipment.TrackingNumber,
        ShipmentStatus = x.Shipment.Status,
        SenderCustomerId = x.SenderCustomerId,
        SenderCode = x.SenderCustomer.Code,
        SenderName = x.SenderCustomer.ArabicName,
        OriginBranchId = x.OriginBranchId,
        OriginBranchName = x.OriginBranch.ArabicName,
        CurrencyId = x.CurrencyId,
        CurrencyCode = x.Currency.Code,
        Status = x.Status,
        StatusBeforeDispute = x.StatusBeforeDispute,
        ExpectedAmount = x.ExpectedAmount,
        CollectedAmount = x.CollectedAmount,
        CollectedAtUtc = x.CollectedAtUtc,
        DriverId = x.DriverId,
        DriverName = x.Driver != null ? x.Driver.Employee.ArabicName : null,
        CollectedByUserId = x.CollectedByUserId,
        CollectionDifferenceReason = x.CollectionDifferenceReason,
        ReceivedCashboxId = x.ReceivedCashboxId,
        ReceivedCashboxName = x.ReceivedCashbox != null ? x.ReceivedCashbox.ArabicName : null,
        ReceiptVoucherId = x.ReceiptVoucherId,
        ReceiptVoucherNumber = x.ReceiptVoucher != null ? x.ReceiptVoucher.VoucherNumber : null,
        ReceivedAtUtc = x.ReceivedAtUtc,
        ShippingChargeDeduction = x.ShippingChargeDeduction,
        CodCommissionDeduction = x.CodCommissionDeduction,
        TaxDeduction = x.TaxDeduction,
        OtherFeesAmount = x.OtherFeesAmount,
        CompensationAmount = x.CompensationAmount,
        ShippingChargeDeducted = x.ShippingChargeDeducted,
        NetPayableAmount = x.NetPayableAmount,
        ReadyAtUtc = x.ReadyAtUtc,
        SettledAmount = x.SettledAmount,
        RemainingPayable = x.NetPayableAmount - x.SettledAmount,
        DeductionsRecognized = x.DeductionsRecognized,
        SettledAtUtc = x.SettledAtUtc,
        OpenSettlementId = x.SettlementLines.Where(l => l.IsOpen).Select(l => (long?)l.SenderSettlementId).FirstOrDefault(),
        OpenSettlementNumber = x.SettlementLines.Where(l => l.IsOpen).Select(l => l.SenderSettlement.SettlementNumber).FirstOrDefault(),
        DisputeReason = x.DisputeReason,
        DisputedAtUtc = x.DisputedAtUtc,
        CancelledAtUtc = x.CancelledAtUtc,
        CreatedAtUtc = x.CreatedAtUtc,
        History = x.History
            .OrderBy(h => h.OccurredAtUtc)
            .ThenBy(h => h.Id)
            .Select(h => new CodHistoryDto
            {
                Id = h.Id,
                TransactionType = h.TransactionType,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                Amount = h.Amount,
                OccurredAtUtc = h.OccurredAtUtc,
                DriverId = h.DriverId,
                CashboxId = h.CashboxId,
                ReferenceType = h.ReferenceType,
                ReferenceId = h.ReferenceId,
                ReferenceNumber = h.ReferenceNumber,
                Notes = h.Notes,
                CreatedByUserId = h.CreatedByUserId
            })
            .ToList()
    };

    /// <summary>Adds the Arabic status names (not translatable to SQL).</summary>
    public static CodTransactionListItemDto WithNames(CodTransactionListItemDto dto) => new()
    {
        Id = dto.Id,
        ShipmentId = dto.ShipmentId,
        ShipmentNumber = dto.ShipmentNumber,
        TrackingNumber = dto.TrackingNumber,
        ShipmentStatus = dto.ShipmentStatus,
        SenderCustomerId = dto.SenderCustomerId,
        SenderName = dto.SenderName,
        DriverId = dto.DriverId,
        DriverName = dto.DriverName,
        CurrencyId = dto.CurrencyId,
        CurrencyCode = dto.CurrencyCode,
        Status = dto.Status,
        StatusName = CodNames.Arabic(dto.Status),
        ExpectedAmount = dto.ExpectedAmount,
        CollectedAmount = dto.CollectedAmount,
        NetPayableAmount = dto.NetPayableAmount,
        SettledAmount = dto.SettledAmount,
        RemainingPayable = dto.RemainingPayable,
        CollectedAtUtc = dto.CollectedAtUtc,
        ReceivedAtUtc = dto.ReceivedAtUtc,
        CreatedAtUtc = dto.CreatedAtUtc
    };

    public static CodTransactionDto WithNames(CodTransactionDto dto) => new()
    {
        Id = dto.Id,
        ShipmentId = dto.ShipmentId,
        ShipmentNumber = dto.ShipmentNumber,
        TrackingNumber = dto.TrackingNumber,
        ShipmentStatus = dto.ShipmentStatus,
        SenderCustomerId = dto.SenderCustomerId,
        SenderCode = dto.SenderCode,
        SenderName = dto.SenderName,
        OriginBranchId = dto.OriginBranchId,
        OriginBranchName = dto.OriginBranchName,
        CurrencyId = dto.CurrencyId,
        CurrencyCode = dto.CurrencyCode,
        Status = dto.Status,
        StatusName = CodNames.Arabic(dto.Status),
        StatusBeforeDispute = dto.StatusBeforeDispute,
        ExpectedAmount = dto.ExpectedAmount,
        CollectedAmount = dto.CollectedAmount,
        CollectedAtUtc = dto.CollectedAtUtc,
        DriverId = dto.DriverId,
        DriverName = dto.DriverName,
        CollectedByUserId = dto.CollectedByUserId,
        CollectionDifferenceReason = dto.CollectionDifferenceReason,
        ReceivedCashboxId = dto.ReceivedCashboxId,
        ReceivedCashboxName = dto.ReceivedCashboxName,
        ReceiptVoucherId = dto.ReceiptVoucherId,
        ReceiptVoucherNumber = dto.ReceiptVoucherNumber,
        ReceivedAtUtc = dto.ReceivedAtUtc,
        ShippingChargeDeduction = dto.ShippingChargeDeduction,
        CodCommissionDeduction = dto.CodCommissionDeduction,
        TaxDeduction = dto.TaxDeduction,
        OtherFeesAmount = dto.OtherFeesAmount,
        CompensationAmount = dto.CompensationAmount,
        ShippingChargeDeducted = dto.ShippingChargeDeducted,
        NetPayableAmount = dto.NetPayableAmount,
        ReadyAtUtc = dto.ReadyAtUtc,
        SettledAmount = dto.SettledAmount,
        RemainingPayable = dto.RemainingPayable,
        DeductionsRecognized = dto.DeductionsRecognized,
        SettledAtUtc = dto.SettledAtUtc,
        OpenSettlementId = dto.OpenSettlementId,
        OpenSettlementNumber = dto.OpenSettlementNumber,
        DisputeReason = dto.DisputeReason,
        DisputedAtUtc = dto.DisputedAtUtc,
        CancelledAtUtc = dto.CancelledAtUtc,
        CreatedAtUtc = dto.CreatedAtUtc,
        History = dto.History.Select(h => new CodHistoryDto
        {
            Id = h.Id,
            TransactionType = h.TransactionType,
            TransactionTypeName = CodNames.Arabic(h.TransactionType),
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            Amount = h.Amount,
            OccurredAtUtc = h.OccurredAtUtc,
            DriverId = h.DriverId,
            CashboxId = h.CashboxId,
            ReferenceType = h.ReferenceType,
            ReferenceId = h.ReferenceId,
            ReferenceNumber = h.ReferenceNumber,
            Notes = h.Notes,
            CreatedByUserId = h.CreatedByUserId
        }).ToList()
    };
}
