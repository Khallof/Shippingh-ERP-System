using R = ShippingERP.Application.IntegrationEvents.AccountRoles;

namespace ShippingERP.Infrastructure.Accounting;

/// <summary>Arabic names of the posting roles (posting rules screen, error messages).</summary>
public static class AccountRoleNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        [R.CashOnHand] = "نقدية الصناديق",
        [R.CashInTransit] = "نقدية محوّلة بين الفروع",
        [R.Bank] = "البنوك",
        [R.DriverCustody] = "عهدة السائقين (مبالغ التحصيل)",
        [R.CodPayableToSenders] = "أمانات المرسلين (الدفع عند الاستلام)",
        [R.ShippingRevenue] = "إيرادات الشحن",
        [R.CodCommissionRevenue] = "عمولات الدفع عند الاستلام",
        [R.TaxPayable] = "الضريبة المستحقة",
        [R.OtherFeesRevenue] = "رسوم مخصومة من المرسلين",
        [R.SenderCompensationExpense] = "تعويضات المرسلين",
        [R.DriverShortageLoss] = "عجز عهدة السائقين",
        [R.CashOverShort] = "فروقات النقدية",
        [R.CashClearing] = "نقدية العملاء قيد الإيداع",
        [R.OtherIncome] = "إيرادات أخرى",
        [R.Expense] = "المصروفات (حسب فئة المصروف)",
        [R.AccountsPayable] = "الموردون",
        [R.EmployeeAdvances] = "سلف الموظفين",
        [R.CustomerAdvances] = "دفعات مقدمة من العملاء",
        [R.OpeningBalanceEquity] = "أرصدة افتتاحية",
        [R.Suspense] = "حساب معلّق",
        [R.AccountsReceivable] = "الذمم المدينة - العملاء",
        [R.AdditionalServiceRevenue] = "إيرادات الخدمات الإضافية",
        [R.CustomerRefund] = "المبالغ المستردة للعملاء"
    };

    public static string Arabic(string role) => Names.TryGetValue(role, out var name) ? name : role;

    /// <summary>The canonical spelling of a role ("cashonhand" -> "CashOnHand").</summary>
    public static string Canonical(string role) =>
        R.All.FirstOrDefault(x => string.Equals(x, role?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? role.Trim();
}
