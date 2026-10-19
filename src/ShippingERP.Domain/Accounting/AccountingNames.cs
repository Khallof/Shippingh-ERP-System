using ShippingERP.Domain.Enums;

namespace ShippingERP.Domain.Accounting;

/// <summary>Arabic display names of the accounting enums (DTOs and error messages).</summary>
public static class AccountingNames
{
    public static string Arabic(AccountType type) => type switch
    {
        AccountType.Assets => "الأصول",
        AccountType.Liabilities => "الخصوم",
        AccountType.Equity => "حقوق الملكية",
        AccountType.Revenue => "الإيرادات",
        AccountType.CostOfRevenue => "تكلفة الإيرادات",
        AccountType.Expenses => "المصاريف",
        _ => type.ToString()
    };

    public static string Arabic(NormalBalance side) => side == NormalBalance.Debit ? "مدين" : "دائن";

    public static string Arabic(CashFlowActivity activity) => activity switch
    {
        CashFlowActivity.None => "النقدية وما في حكمها",
        CashFlowActivity.Operating => "الأنشطة التشغيلية",
        CashFlowActivity.Investing => "الأنشطة الاستثمارية",
        CashFlowActivity.Financing => "الأنشطة التمويلية",
        _ => activity.ToString()
    };

    public static string Arabic(FiscalYearStatus status) => status == FiscalYearStatus.Open ? "مفتوحة" : "مقفلة";

    public static string Arabic(AccountingPeriodStatus status) => status == AccountingPeriodStatus.Open ? "مفتوحة" : "مقفلة";

    public static string Arabic(JournalEntryStatus status) => status switch
    {
        JournalEntryStatus.Draft => "مسودة",
        JournalEntryStatus.Submitted => "بانتظار الاعتماد",
        JournalEntryStatus.Approved => "معتمد",
        JournalEntryStatus.Posted => "مرحّل",
        JournalEntryStatus.Reversed => "معكوس",
        JournalEntryStatus.Rejected => "مرفوض",
        _ => status.ToString()
    };

    public static string Arabic(JournalType type) => type switch
    {
        JournalType.General => "قيد يومية عام",
        JournalType.Opening => "قيد افتتاحي",
        JournalType.Adjustment => "قيد تسوية",
        JournalType.Reversal => "قيد عكسي",
        JournalType.Closing => "قيد إقفال",
        JournalType.Sales => "يومية المبيعات والفواتير",
        JournalType.CustomerReceipts => "يومية مقبوضات العملاء",
        JournalType.Cod => "يومية الدفع عند الاستلام",
        JournalType.Cash => "يومية الصناديق",
        JournalType.SenderSettlement => "يومية تسويات المرسلين",
        JournalType.Expense => "يومية المصروفات",
        JournalType.Bank => "يومية البنك",
        _ => type.ToString()
    };

    public static string Arabic(DimensionType type) => type switch
    {
        DimensionType.CostCenter => "مركز تكلفة",
        DimensionType.Project => "مشروع",
        DimensionType.Vehicle => "مركبة",
        DimensionType.Department => "قسم",
        _ => type.ToString()
    };

    public static string Arabic(PostingEventStatus status) => status switch
    {
        PostingEventStatus.Posted => "مرحّل",
        PostingEventStatus.Skipped => "بلا أثر محاسبي",
        PostingEventStatus.Duplicate => "مكرر (لم يُرحّل مرة ثانية)",
        PostingEventStatus.Failed => "فشل الترحيل",
        _ => status.ToString()
    };

    public static string Arabic(BankReconciliationStatus status) => status == BankReconciliationStatus.Draft ? "مسودة" : "مكتملة";

    public static string Arabic(BankReconciliationLineType type) => type switch
    {
        BankReconciliationLineType.Matched => "مطابق",
        BankReconciliationLineType.BankOnly => "في كشف البنك فقط",
        BankReconciliationLineType.BookOnly => "في الدفاتر فقط",
        _ => type.ToString()
    };
}
