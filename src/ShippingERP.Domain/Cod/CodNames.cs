using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Cod;

/// <summary>Arabic display names of the COD and cash enums (DTOs, PDFs, error messages).</summary>
public static class CodNames
{
    public static string Arabic(CODStatus status) => status switch
    {
        CODStatus.Pending => "بانتظار التوصيل",
        CODStatus.AssignedToDriver => "مع السائق للتوصيل",
        CODStatus.CollectedByDriver => "محصّل لدى السائق",
        CODStatus.ReceivedAtBranchCashbox => "مستلم في صندوق الفرع",
        CODStatus.ReadyForSenderSettlement => "جاهز للتسوية مع المرسل",
        CODStatus.PartiallySettled => "مسوّى جزئياً",
        CODStatus.SettledToSender => "مسوّى مع المرسل",
        CODStatus.Cancelled => "ملغى",
        CODStatus.Disputed => "متنازع عليه",
        _ => status.ToString()
    };

    public static string Arabic(CODTransactionType type) => type switch
    {
        CODTransactionType.Registered => "تسجيل مبلغ التحصيل",
        CODTransactionType.AssignedToDriver => "خروج مع السائق",
        CODTransactionType.Unassigned => "عودة إلى الانتظار",
        CODTransactionType.ExpectedAmountChanged => "تعديل مبلغ التحصيل",
        CODTransactionType.Cancelled => "إلغاء",
        CODTransactionType.Reopened => "إعادة تفعيل",
        CODTransactionType.Collected => "تحصيل من المستلم",
        CODTransactionType.CollectedAtCounter => "تحصيل في الفرع",
        CODTransactionType.HandedOver => "تسليم للصندوق",
        CODTransactionType.HandoverReversed => "إلغاء الاستلام في الصندوق",
        CODTransactionType.MarkedReady => "جاهز للتسوية",
        CODTransactionType.SettlementPaid => "دفع للمرسل",
        CODTransactionType.SettlementReversed => "عكس تسوية",
        CODTransactionType.CollectionCorrected => "تصحيح المبلغ المحصّل",
        CODTransactionType.CollectionReversed => "إلغاء التحصيل",
        CODTransactionType.SenderFeeAdded => "رسم إضافي على المرسل",
        CODTransactionType.SenderCompensationAdded => "تعويض للمرسل",
        CODTransactionType.DisputeOpened => "فتح نزاع",
        CODTransactionType.DisputeResolved => "حل النزاع",
        _ => type.ToString()
    };

    public static string Arabic(CODAdjustmentType type) => type switch
    {
        CODAdjustmentType.DriverShortageWriteOff => "شطب عجز سائق",
        CODAdjustmentType.DriverExcessCash => "زيادة نقدية لدى السائق",
        CODAdjustmentType.CollectionCorrection => "تصحيح مبلغ محصّل",
        CODAdjustmentType.CollectionReversal => "إلغاء تحصيل مسجّل بالخطأ",
        CODAdjustmentType.SenderFee => "رسم إضافي على المرسل",
        CODAdjustmentType.SenderCompensation => "تعويض للمرسل",
        _ => type.ToString()
    };

    public static string Arabic(CODAdjustmentStatus status) => status switch
    {
        CODAdjustmentStatus.PendingApproval => "بانتظار الموافقة",
        CODAdjustmentStatus.Approved => "معتمدة",
        CODAdjustmentStatus.Rejected => "مرفوضة",
        _ => status.ToString()
    };

    public static string Arabic(SettlementStatus status) => status switch
    {
        SettlementStatus.Draft => "مسودة",
        SettlementStatus.Approved => "معتمدة",
        SettlementStatus.Paid => "مدفوعة",
        SettlementStatus.Cancelled => "ملغاة",
        SettlementStatus.Reversed => "معكوسة",
        _ => status.ToString()
    };

    public static string Arabic(DriverCashMovementType type) => type switch
    {
        DriverCashMovementType.CodCollected => "تحصيل",
        DriverCashMovementType.Handover => "تسليم للصندوق",
        DriverCashMovementType.HandoverReversed => "إلغاء تسليم",
        DriverCashMovementType.CollectionCorrected => "تصحيح تحصيل",
        DriverCashMovementType.CollectionReversed => "إلغاء تحصيل",
        DriverCashMovementType.ShortageWrittenOff => "شطب عجز",
        DriverCashMovementType.ExcessRecorded => "تسجيل زيادة",
        _ => type.ToString()
    };

    public static string Arabic(CashboxTransactionType type) => type switch
    {
        CashboxTransactionType.OpeningBalance => "رصيد افتتاحي",
        CashboxTransactionType.DriverHandover => "استلام من سائق",
        CashboxTransactionType.CounterCodCollection => "تحصيل في الفرع",
        CashboxTransactionType.CashReceipt => "سند قبض",
        CashboxTransactionType.CashPayment => "سند صرف",
        CashboxTransactionType.SenderSettlementPayment => "دفع تسوية مرسل",
        CashboxTransactionType.TransferOut => "تحويل صادر",
        CashboxTransactionType.TransferIn => "تحويل وارد",
        CashboxTransactionType.ClosingShortage => "عجز إقفال",
        CashboxTransactionType.ClosingExcess => "زيادة إقفال",
        CashboxTransactionType.Reversal => "عكس حركة",
        _ => type.ToString()
    };

    public static string Arabic(CashReceiptType type) => type switch
    {
        CashReceiptType.DriverHandover => "استلام عهدة سائق",
        CashReceiptType.CounterCodCollection => "تحصيل دفع عند الاستلام في الفرع",
        CashReceiptType.CustomerPayment => "دفعة عميل نقدية",
        CashReceiptType.OtherIncome => "إيراد آخر",
        CashReceiptType.Other => "أخرى",
        _ => type.ToString()
    };

    public static string Arabic(CashPaymentType type) => type switch
    {
        CashPaymentType.SenderSettlement => "تسوية مبالغ تحصيل لمرسل",
        CashPaymentType.Expense => "مصروف",
        CashPaymentType.SupplierPayment => "دفعة لمورد",
        CashPaymentType.EmployeeAdvance => "سلفة موظف",
        CashPaymentType.CustomerRefund => "رد مبلغ لعميل",
        CashPaymentType.Other => "أخرى",
        _ => type.ToString()
    };

    public static string Arabic(CashVoucherStatus status) => status == CashVoucherStatus.Posted ? "مرحّل" : "ملغى";

    public static string Arabic(CashTransferType type) => type switch
    {
        CashTransferType.BetweenCashboxes => "بين صناديق الفرع",
        CashTransferType.BetweenBranches => "بين الفروع",
        CashTransferType.CashboxToBank => "إيداع في البنك",
        _ => type.ToString()
    };

    public static string Arabic(CashTransferStatus status) => status switch
    {
        CashTransferStatus.PendingApproval => "بانتظار الموافقة",
        CashTransferStatus.InTransit => "قيد النقل",
        CashTransferStatus.Completed => "مكتمل",
        CashTransferStatus.Rejected => "مرفوض",
        CashTransferStatus.Cancelled => "ملغى",
        _ => status.ToString()
    };

    public static string Arabic(CashboxClosingStatus status) => status switch
    {
        CashboxClosingStatus.Closed => "مقفل",
        CashboxClosingStatus.PendingApproval => "فرق بانتظار الموافقة",
        CashboxClosingStatus.Approved => "مقفل بفرق معتمد",
        CashboxClosingStatus.Rejected => "مرفوض (يُعاد العد)",
        _ => status.ToString()
    };
}
