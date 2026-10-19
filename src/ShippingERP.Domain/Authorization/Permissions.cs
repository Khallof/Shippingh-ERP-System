namespace ShippingERP.Domain.Authorization;

/// <summary>
/// The single source of truth for permission codes.
///
/// - Use the constants in code: [HasPermission(Permissions.Shipments.Create)]
/// - The seeder inserts any code from <see cref="All"/> that is missing in the Permissions table,
///   so adding a permission = add a constant here + add it to All + restart the API.
/// - Never rename an existing code: role assignments and issued tokens refer to it.
/// </summary>
public static class Permissions
{
    public static class Users
    {
        public const string View = "Users.View";
        public const string Manage = "Users.Manage";
    }

    public static class Roles
    {
        public const string Manage = "Roles.Manage";
    }

    public static class Customers
    {
        public const string View = "Customers.View";
        public const string Manage = "Customers.Manage";
    }

    public static class Shipments
    {
        public const string View = "Shipments.View";
        public const string Create = "Shipments.Create";
        public const string Update = "Shipments.Update";
        public const string ChangeStatus = "Shipments.ChangeStatus";
        public const string AssignDriver = "Shipments.AssignDriver";

        // ----- Step 4 -----

        /// <summary>Cancel a shipment that has not left the origin branch.</summary>
        public const string Cancel = "Shipments.Cancel";

        /// <summary>Confirm a delivery (proof of delivery) or record a failed attempt.</summary>
        public const string ConfirmDelivery = "Shipments.ConfirmDelivery";

        /// <summary>Correct a wrong status outside the normal rules (reason required, fully audited).</summary>
        public const string CorrectStatus = "Shipments.CorrectStatus";

        /// <summary>Import shipments from Excel.</summary>
        public const string Import = "Shipments.Import";

        /// <summary>Enter a manual shipping charge (when no pricing rule applies) or give a discount (a reason is required).</summary>
        public const string OverridePrice = "Shipments.OverridePrice";
    }

    // ----- Step 4: operations -----

    /// <summary>Warehouse check-in / check-out manifests.</summary>
    public static class Warehouse
    {
        public const string ManageMovements = "Warehouse.ManageMovements";
    }

    /// <summary>Branch transfer manifests.</summary>
    public static class Transfers
    {
        public const string View = "Transfers.View";
        public const string Manage = "Transfers.Manage";
        public const string Receive = "Transfers.Receive";
    }

    /// <summary>Driver delivery manifests (drivers see their own manifests with Shipments.View).</summary>
    public static class DeliveryManifests
    {
        public const string Manage = "DeliveryManifests.Manage";
    }

    public static class Returns
    {
        public const string Manage = "Returns.Manage";
    }

    public static class DamageCases
    {
        public const string Manage = "DamageCases.Manage";
    }

    /// <summary>
    /// Cash on delivery. Step 6 adds the handover, approval and adjustment permissions. A driver-only user (Driver
    /// role without Shipments.AssignDriver) sees only his own COD shipments and his own cash balance.
    /// </summary>
    public static class Cod
    {
        public const string View = "COD.View";

        /// <summary>Record the COD amount collected from the recipient.</summary>
        public const string Collect = "COD.Collect";

        /// <summary>Mark received COD ready for settlement and create sender settlements (step 6 meaning).</summary>
        public const string Settle = "COD.Settle";

        // ----- Step 6 -----

        /// <summary>Receive a driver's cash into a cashbox (driver handover) or a counter COD collection.</summary>
        public const string Handover = "COD.Handover";

        /// <summary>Approve, cancel or reverse sender settlements.</summary>
        public const string ApproveSettlement = "COD.ApproveSettlement";

        /// <summary>Pay an approved sender settlement (cash from a cashbox or bank transfer).</summary>
        public const string PaySettlement = "COD.PaySettlement";

        /// <summary>Request COD / driver cash adjustments and open disputes.</summary>
        public const string Adjust = "COD.Adjust";

        /// <summary>Approve or reject adjustments and resolve disputes.</summary>
        public const string ApproveAdjustment = "COD.ApproveAdjustment";
    }

    // ----- Step 6: cash management -----

    /// <summary>Branch cashboxes, their transactions, daily closings and bank accounts.</summary>
    public static class Cashboxes
    {
        public const string View = "Cashboxes.View";

        /// <summary>Create and edit cashboxes and bank accounts.</summary>
        public const string Manage = "Cashboxes.Manage";

        /// <summary>Close the day of a cashbox (count the cash).</summary>
        public const string Close = "Cashboxes.Close";

        /// <summary>Approve or reject a closing with a cash difference.</summary>
        public const string ApproveClosing = "Cashboxes.ApproveClosing";
    }

    /// <summary>Cash receipt and payment vouchers (سندات القبض والصرف النقدية).</summary>
    public static class CashVouchers
    {
        public const string Create = "CashVouchers.Create";

        /// <summary>Cancel a posted voucher (adds a reversing cashbox transaction).</summary>
        public const string Cancel = "CashVouchers.Cancel";
    }

    public static class CashTransfers
    {
        public const string Create = "CashTransfers.Create";
        public const string Approve = "CashTransfers.Approve";
        public const string Receive = "CashTransfers.Receive";
    }

    public static class Invoices
    {
        public const string View = "Invoices.View";
        public const string Create = "Invoices.Create";

        // ----- Step 5 -----

        /// <summary>Issue a draft invoice (gives it its number and debits the customer account).</summary>
        public const string Issue = "Invoices.Issue";

        /// <summary>Cancel a draft, or an issued invoice that has no payment and no credit note.</summary>
        public const string Cancel = "Invoices.Cancel";

        /// <summary>Issue credit notes against issued invoices.</summary>
        public const string CreditNotes = "Invoices.CreditNotes";

        /// <summary>Export invoices to Excel and print / download invoice PDFs.</summary>
        public const string Export = "Invoices.Export";
    }

    // ----- Step 5: receivables -----

    /// <summary>Customer payments (سندات القبض).</summary>
    public static class Payments
    {
        public const string View = "Payments.View";
        public const string Create = "Payments.Create";
    }

    /// <summary>Customer account statements and balances.</summary>
    public static class Receivables
    {
        public const string View = "Receivables.View";
    }

    public static class Accounting
    {
        public const string View = "Accounting.View";
        public const string CreateJournal = "Accounting.CreateJournal";
        public const string ApproveJournal = "Accounting.ApproveJournal";

        // Step 7: double-entry accounting.
        public const string PostJournal = "Accounting.PostJournal";
        public const string ReverseJournal = "Accounting.ReverseJournal";
        public const string ManageChart = "Accounting.ManageChart";
        public const string ManagePeriods = "Accounting.ManagePeriods";
        public const string BankReconciliation = "Accounting.BankReconciliation";
        public const string ManagePostings = "Accounting.ManagePostings";
    }

    public static class Expenses
    {
        public const string View = "Expenses.View";
        public const string Create = "Expenses.Create";
        public const string Approve = "Expenses.Approve";
    }

    public static class Reports
    {
        public const string ViewFinancial = "Reports.ViewFinancial";
        public const string ViewOperational = "Reports.ViewOperational";
    }

    public static class Branches
    {
        /// <summary>See and manage data of every branch. Without it a user only sees their own branch.</summary>
        public const string ViewAll = "Branches.ViewAll";
        public const string Manage = "Branches.Manage";
    }

    // ----- Step 3: master data -----

    /// <summary>
    /// Reference data used by everybody: countries, cities, zones, service types, payment methods,
    /// currencies, exchange rates, tax rates, expense categories, company profile.
    /// Reading them only requires being signed in; changing them requires MasterData.Manage.
    /// </summary>
    public static class MasterData
    {
        public const string Manage = "MasterData.Manage";
    }

    public static class Pricing
    {
        public const string View = "Pricing.View";
        public const string Manage = "Pricing.Manage";

        /// <summary>Step 5: approve or reject manual prices above the approval threshold (and apply them directly).</summary>
        public const string ApproveOverride = "Pricing.ApproveOverride";
    }

    public static class Suppliers
    {
        public const string View = "Suppliers.View";
        public const string Manage = "Suppliers.Manage";
    }

    /// <summary>Employees and driver profiles.</summary>
    public static class Employees
    {
        public const string View = "Employees.View";
        public const string Manage = "Employees.Manage";
    }

    public static class Vehicles
    {
        public const string View = "Vehicles.View";
        public const string Manage = "Vehicles.Manage";
    }

    /// <summary>Step 8: read the audit log (who did what and when).</summary>
    public static class AuditLogs
    {
        public const string View = "AuditLogs.View";
    }

    /// <summary>Every permission with its module and Arabic description.</summary>
    public static IReadOnlyList<PermissionDefinition> All { get; } = new List<PermissionDefinition>
    {
        new(Users.View, "Users", "عرض المستخدمين"),
        new(Users.Manage, "Users", "إدارة المستخدمين (إضافة، تعديل، تفعيل، إيقاف)"),
        new(Roles.Manage, "Roles", "إدارة الأدوار والصلاحيات"),

        new(Customers.View, "Customers", "عرض العملاء"),
        new(Customers.Manage, "Customers", "إدارة العملاء"),

        new(Shipments.View, "Shipments", "عرض الشحنات"),
        new(Shipments.Create, "Shipments", "إنشاء شحنة"),
        new(Shipments.Update, "Shipments", "تعديل شحنة"),
        new(Shipments.ChangeStatus, "Shipments", "تغيير حالة الشحنة"),
        new(Shipments.AssignDriver, "Shipments", "إسناد الشحنة إلى سائق"),
        new(Shipments.Cancel, "Shipments", "إلغاء شحنة"),
        new(Shipments.ConfirmDelivery, "Shipments", "تأكيد التسليم وتسجيل محاولات التسليم الفاشلة"),
        new(Shipments.CorrectStatus, "Shipments", "تصحيح حالة شحنة خارج المسار الطبيعي"),
        new(Shipments.Import, "Shipments", "استيراد الشحنات من Excel"),
        new(Shipments.OverridePrice, "Shipments", "إدخال سعر شحن يدوي ومنح خصم"),

        new(Warehouse.ManageMovements, "Warehouse", "إدخال وإخراج الشحنات من المستودع"),
        new(Transfers.View, "Transfers", "عرض التحويلات بين الفروع"),
        new(Transfers.Manage, "Transfers", "إنشاء وإرسال التحويلات بين الفروع"),
        new(Transfers.Receive, "Transfers", "استلام التحويلات الواردة"),
        new(DeliveryManifests.Manage, "DeliveryManifests", "إنشاء وإدارة بيانات التوزيع للسائقين"),
        new(Returns.Manage, "Returns", "إدارة المرتجعات"),
        new(DamageCases.Manage, "DamageCases", "إدارة حالات التلف والفقدان"),

        new(Cod.View, "COD", "عرض مبالغ الدفع عند الاستلام"),
        new(Cod.Collect, "COD", "تحصيل مبالغ الدفع عند الاستلام"),
        new(Cod.Settle, "COD", "تسوية مبالغ الدفع عند الاستلام مع العملاء"),
        new(Cod.Handover, "COD", "استلام نقدية السائقين والتحصيل في الفرع إلى الصندوق"),
        new(Cod.ApproveSettlement, "COD", "اعتماد تسويات المرسلين وإلغاؤها وعكسها"),
        new(Cod.PaySettlement, "COD", "دفع تسويات المرسلين"),
        new(Cod.Adjust, "COD", "طلب تسويات التحصيل وعهدة السائقين وفتح النزاعات"),
        new(Cod.ApproveAdjustment, "COD", "اعتماد تسويات التحصيل وحل النزاعات"),

        new(Cashboxes.View, "Cashboxes", "عرض الصناديق وحركاتها وتقارير النقدية"),
        new(Cashboxes.Manage, "Cashboxes", "إدارة الصناديق والحسابات البنكية"),
        new(Cashboxes.Close, "Cashboxes", "الإقفال اليومي للصناديق"),
        new(Cashboxes.ApproveClosing, "Cashboxes", "اعتماد فروقات الإقفال اليومي"),
        new(CashVouchers.Create, "CashVouchers", "تسجيل سندات القبض والصرف النقدية"),
        new(CashVouchers.Cancel, "CashVouchers", "إلغاء سندات القبض والصرف النقدية"),
        new(CashTransfers.Create, "CashTransfers", "طلب تحويلات النقدية"),
        new(CashTransfers.Approve, "CashTransfers", "اعتماد تحويلات النقدية أو رفضها"),
        new(CashTransfers.Receive, "CashTransfers", "استلام تحويلات النقدية الواردة"),

        new(Invoices.View, "Invoices", "عرض الفواتير"),
        new(Invoices.Create, "Invoices", "إنشاء الفواتير"),
        new(Invoices.Issue, "Invoices", "إصدار الفواتير (ترقيمها وترحيلها إلى حساب العميل)"),
        new(Invoices.Cancel, "Invoices", "إلغاء الفواتير غير المدفوعة"),
        new(Invoices.CreditNotes, "Invoices", "إصدار الإشعارات الدائنة"),
        new(Invoices.Export, "Invoices", "طباعة الفواتير وتصديرها إلى Excel"),

        new(Payments.View, "Payments", "عرض سندات القبض"),
        new(Payments.Create, "Payments", "تسجيل دفعات العملاء وتخصيصها على الفواتير"),
        new(Receivables.View, "Receivables", "عرض كشوف حسابات العملاء وأرصدتهم"),

        new(Accounting.View, "Accounting", "عرض الحسابات والقيود"),
        new(Accounting.CreateJournal, "Accounting", "إنشاء قيود يومية"),
        new(Accounting.ApproveJournal, "Accounting", "اعتماد قيود اليومية أو رفضها"),
        new(Accounting.PostJournal, "Accounting", "ترحيل القيود المعتمدة إلى دفتر الأستاذ"),
        new(Accounting.ReverseJournal, "Accounting", "عكس القيود المرحّلة"),
        new(Accounting.ManageChart, "Accounting", "إدارة دليل الحسابات ومراكز التكلفة وقواعد الترحيل الآلي"),
        new(Accounting.ManagePeriods, "Accounting", "إدارة السنوات المالية وإقفال الفترات وإعادة فتحها"),
        new(Accounting.BankReconciliation, "Accounting", "التسويات البنكية وكشوف البنك"),
        new(Accounting.ManagePostings, "Accounting", "متابعة الترحيل الآلي وإعادة محاولة الأحداث الفاشلة"),

        new(Expenses.View, "Expenses", "عرض المصروفات"),
        new(Expenses.Create, "Expenses", "تسجيل المصروفات"),
        new(Expenses.Approve, "Expenses", "اعتماد المصروفات"),

        new(Reports.ViewFinancial, "Reports", "عرض التقارير المالية"),
        new(Reports.ViewOperational, "Reports", "عرض التقارير التشغيلية"),

        new(Branches.ViewAll, "Branches", "الاطلاع على بيانات جميع الفروع"),
        new(Branches.Manage, "Branches", "إدارة الفروع والمستودعات"),

        new(MasterData.Manage, "MasterData", "إدارة البيانات الأساسية (الدول، المدن، المناطق، العملات، الضرائب...)"),

        new(Pricing.View, "Pricing", "عرض خطط التسعير"),
        new(Pricing.Manage, "Pricing", "إدارة خطط التسعير وقواعدها"),
        new(Pricing.ApproveOverride, "Pricing", "اعتماد تعديلات الأسعار اليدوية الكبيرة"),

        new(Suppliers.View, "Suppliers", "عرض الموردين"),
        new(Suppliers.Manage, "Suppliers", "إدارة الموردين"),

        new(Employees.View, "Employees", "عرض الموظفين والسائقين"),
        new(Employees.Manage, "Employees", "إدارة الموظفين والسائقين"),

        new(Vehicles.View, "Vehicles", "عرض المركبات"),
        new(Vehicles.Manage, "Vehicles", "إدارة المركبات"),

        new(AuditLogs.View, "AuditLogs", "عرض سجل التدقيق")
    };

    private static readonly HashSet<string> AllCodes =
        new(All.Select(permission => permission.Code), StringComparer.Ordinal);

    /// <summary>True if the code exists in the catalog (case-sensitive).</summary>
    public static bool IsKnown(string code) => AllCodes.Contains(code);
}
