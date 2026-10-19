import type {
  AccountType, AuditAction, BranchTransferStatus, CashPaymentType, CashReceiptType, CashTransferStatus, CashTransferType,
  CashVoucherStatus, CODAdjustmentStatus, CODAdjustmentType, CODStatus, CustomerRole, CustomerType, DriverManifestStatus,
  InvoiceStatus, JournalEntryStatus, JournalType, NormalBalance, PaymentMethodKind, PostingEventStatus, PriceOverrideStatus,
  SettlementStatus, ShipmentReturnStatus, ShipmentReturnType, ShipmentStatus, ShipmentStatusChangeSource, VehicleType,
  AccountingPeriodStatus, FiscalYearStatus, CashboxClosingStatus, DamageCaseStatus, PriceConditionType, ShipmentAttachmentType, BranchTransferItemStatus, DeliveryResult,
  ChargesPayer, PricingBasis
} from '@/api/generated';
import type { Language } from './index';

type Labels<T extends string> = Record<T, [arabic: string, english: string]>;

/** Arabic / English names of the API enums (the API sends the enum NAME, e.g. "OutForDelivery"). */
export const enumLabels = {
  ShipmentStatus: {
    Created: ['مُسجّلة', 'Created'], PickedUp: ['تم الاستلام من المرسل', 'Picked up'], ReceivedAtBranch: ['في الفرع', 'At branch'],
    InWarehouse: ['في المستودع', 'In warehouse'], InTransit: ['في الطريق بين الفروع', 'In transit'],
    ArrivedAtDestination: ['وصلت فرع الوجهة', 'At destination'], OutForDelivery: ['خارج للتوزيع', 'Out for delivery'],
    Delivered: ['تم التسليم', 'Delivered'], DeliveryFailed: ['فشل التسليم', 'Delivery failed'], Returned: ['مرتجعة', 'Returned'],
    Cancelled: ['ملغاة', 'Cancelled'], LostOrDamaged: ['مفقودة / تالفة', 'Lost or damaged']
  } satisfies Labels<ShipmentStatus>,
  ShipmentStatusChangeSource: {
    Creation: ['التسجيل', 'Creation'], Import: ['الاستيراد', 'Import'], Manual: ['يدوي', 'Manual'], WarehouseManifest: ['بيان المستودع', 'Warehouse manifest'],
    BranchTransfer: ['تحويل بين الفروع', 'Branch transfer'], DriverManifest: ['كشف توزيع', 'Driver manifest'], Delivery: ['التسليم', 'Delivery'],
    Return: ['مرتجع', 'Return'], DamageCase: ['حالة تلف', 'Damage case'], Cancellation: ['إلغاء', 'Cancellation'], Correction: ['تصحيح', 'Correction']
  } satisfies Labels<ShipmentStatusChangeSource>,
  ShipmentAttachmentType: {
    Document: ['مستند', 'Document'], PackagePhoto: ['صورة الطرد', 'Package photo'], CommercialInvoice: ['فاتورة تجارية', 'Commercial invoice'],
    Signature: ['توقيع', 'Signature'], DeliveryPhoto: ['صورة التسليم', 'Delivery photo'], DamageEvidence: ['دليل تلف', 'Damage evidence'], Other: ['أخرى', 'Other']
  } satisfies Labels<ShipmentAttachmentType>,
  CODStatus: {
    Pending: ['بانتظار التسليم', 'Pending'], AssignedToDriver: ['مع سائق للتحصيل', 'Assigned to driver'], CollectedByDriver: ['حصّله السائق', 'Collected by driver'],
    ReceivedAtBranchCashbox: ['في صندوق الفرع', 'In branch cashbox'], ReadyForSenderSettlement: ['جاهز للتسوية', 'Ready for settlement'],
    PartiallySettled: ['مسوّى جزئياً', 'Partially settled'], SettledToSender: ['مسوّى للمرسل', 'Settled to sender'], Cancelled: ['ملغى', 'Cancelled'],
    Disputed: ['متنازع عليه', 'Disputed']
  } satisfies Labels<CODStatus>,
  CODAdjustmentType: {
    DriverShortageWriteOff: ['شطب عجز سائق', 'Driver shortage write-off'], DriverExcessCash: ['زيادة نقدية لدى السائق', 'Driver excess cash'],
    CollectionCorrection: ['تصحيح تحصيل', 'Collection correction'], CollectionReversal: ['عكس تحصيل', 'Collection reversal'],
    SenderFee: ['رسم على المرسل', 'Sender fee'], SenderCompensation: ['تعويض للمرسل', 'Sender compensation']
  } satisfies Labels<CODAdjustmentType>,
  CODAdjustmentStatus: { PendingApproval: ['بانتظار الاعتماد', 'Pending approval'], Approved: ['معتمد', 'Approved'], Rejected: ['مرفوض', 'Rejected'] } satisfies Labels<CODAdjustmentStatus>,
  InvoiceStatus: {
    Draft: ['مسودة', 'Draft'], Issued: ['صادرة', 'Issued'], PartiallyPaid: ['مدفوعة جزئياً', 'Partially paid'], Paid: ['مدفوعة', 'Paid'],
    Overdue: ['متأخرة', 'Overdue'], Cancelled: ['ملغاة', 'Cancelled'], Credited: ['مُشعرة دائن', 'Credited']
  } satisfies Labels<InvoiceStatus>,
  JournalEntryStatus: {
    Draft: ['مسودة', 'Draft'], Submitted: ['مقدّم للاعتماد', 'Submitted'], Approved: ['معتمد', 'Approved'], Posted: ['مُرحّل', 'Posted'],
    Reversed: ['معكوس', 'Reversed'], Rejected: ['مرفوض', 'Rejected']
  } satisfies Labels<JournalEntryStatus>,
  JournalType: {
    General: ['عام', 'General'], Opening: ['افتتاحي', 'Opening'], Adjustment: ['تسوية', 'Adjustment'], Reversal: ['عكسي', 'Reversal'],
    Closing: ['إقفال', 'Closing'], Sales: ['مبيعات', 'Sales'], CustomerReceipts: ['مقبوضات العملاء', 'Customer receipts'], Cod: ['تحصيل عند الاستلام', 'COD'],
    Cash: ['نقدية', 'Cash'], SenderSettlement: ['تسوية مرسلين', 'Sender settlement'], Expense: ['مصروفات', 'Expense'], Bank: ['بنك', 'Bank']
  } satisfies Labels<JournalType>,
  SettlementStatus: {
    Draft: ['مسودة', 'Draft'], Approved: ['معتمدة', 'Approved'], Paid: ['مدفوعة', 'Paid'], Cancelled: ['ملغاة', 'Cancelled'], Reversed: ['معكوسة', 'Reversed']
  } satisfies Labels<SettlementStatus>,
  CashVoucherStatus: { Posted: ['مُرحّل', 'Posted'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<CashVoucherStatus>,
  CashTransferStatus: {
    PendingApproval: ['بانتظار الاعتماد', 'Pending approval'], InTransit: ['في الطريق', 'In transit'], Completed: ['مكتمل', 'Completed'],
    Rejected: ['مرفوض', 'Rejected'], Cancelled: ['ملغى', 'Cancelled']
  } satisfies Labels<CashTransferStatus>,
  CashTransferType: {
    BetweenCashboxes: ['بين صناديق', 'Between cashboxes'], BetweenBranches: ['بين فروع', 'Between branches'], CashboxToBank: ['إيداع بنكي', 'Cashbox to bank']
  } satisfies Labels<CashTransferType>,
  CashReceiptType: {
    DriverHandover: ['تسليم عهدة سائق', 'Driver handover'], CounterCodCollection: ['تحصيل في الفرع', 'Counter COD'], CustomerPayment: ['دفعة عميل', 'Customer payment'],
    OtherIncome: ['إيراد آخر', 'Other income'], Other: ['أخرى', 'Other']
  } satisfies Labels<CashReceiptType>,
  CashPaymentType: {
    SenderSettlement: ['تسوية مرسل', 'Sender settlement'], Expense: ['مصروف', 'Expense'], SupplierPayment: ['دفعة مورد', 'Supplier payment'],
    EmployeeAdvance: ['سلفة موظف', 'Employee advance'], CustomerRefund: ['رد مبلغ لعميل', 'Customer refund'], Other: ['أخرى', 'Other']
  } satisfies Labels<CashPaymentType>,
  CashboxClosingStatus: {
    Closed: ['مُقفل', 'Closed'], PendingApproval: ['بانتظار الاعتماد', 'Pending approval'], Approved: ['معتمد', 'Approved'], Rejected: ['مرفوض', 'Rejected']
  } satisfies Labels<CashboxClosingStatus>,
  DriverManifestStatus: { Draft: ['مسودة', 'Draft'], Dispatched: ['خرج للتوزيع', 'Dispatched'], Closed: ['مُغلق', 'Closed'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<DriverManifestStatus>,
  BranchTransferStatus: {
    Draft: ['مسودة', 'Draft'], Dispatched: ['مُرسل', 'Dispatched'], Received: ['مُستلم', 'Received'], PartiallyReceived: ['مستلم جزئياً', 'Partially received'],
    Cancelled: ['ملغى', 'Cancelled']
  } satisfies Labels<BranchTransferStatus>,
  BranchTransferItemStatus: { Pending: ['بانتظار الإرسال', 'Pending'], InTransit: ['في الطريق', 'In transit'], Received: ['مستلمة', 'Received'], Missing: ['مفقودة', 'Missing'] } satisfies Labels<BranchTransferItemStatus>,
  DeliveryResult: { Pending: ['قيد التوزيع', 'Pending'], Delivered: ['سُلّمت', 'Delivered'], Failed: ['فشلت', 'Failed'] } satisfies Labels<DeliveryResult>,
  ShipmentReturnStatus: { Open: ['مفتوح', 'Open'], Completed: ['مكتمل', 'Completed'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<ShipmentReturnStatus>,
  ShipmentReturnType: {
    FailedDelivery: ['فشل التسليم', 'Failed delivery'], RecipientRefused: ['رفض المستلم', 'Recipient refused'], SenderRequest: ['طلب المرسل', 'Sender request'],
    AfterDelivery: ['بعد التسليم', 'After delivery']
  } satisfies Labels<ShipmentReturnType>,
  DamageCaseStatus: { Open: ['مفتوحة', 'Open'], UnderInvestigation: ['قيد التحقيق', 'Under investigation'], Resolved: ['محلولة', 'Resolved'] } satisfies Labels<DamageCaseStatus>,
  PricingBasis: { Weight: ['بالوزن', 'By weight'], Volume: ['بالحجم', 'By volume'] } satisfies Labels<PricingBasis>,
  ChargesPayer: { Sender: ['على المرسل', 'Sender pays'], Recipient: ['على المستلم', 'Recipient pays'] } satisfies Labels<ChargesPayer>,
  PriceOverrideStatus: { Pending: ['بانتظار الاعتماد', 'Pending'], Approved: ['معتمد', 'Approved'], Rejected: ['مرفوض', 'Rejected'], Cancelled: ['ملغى', 'Cancelled'] } satisfies Labels<PriceOverrideStatus>,
  PriceConditionType: {
    OriginZone: ['منطقة المصدر', 'Origin zone'], DestinationZone: ['منطقة الوجهة', 'Destination zone'], OriginCity: ['مدينة المصدر', 'Origin city'],
    DestinationCity: ['مدينة الوجهة', 'Destination city'], ServiceType: ['نوع البضاعة', 'Goods type'], ChargeableWeight: ['الوزن المحتسب', 'Chargeable weight'],
    PackageCount: ['عدد الطرود', 'Package count']
  } satisfies Labels<PriceConditionType>,
  AccountType: {
    Assets: ['أصول', 'Assets'], Liabilities: ['خصوم', 'Liabilities'], Equity: ['حقوق ملكية', 'Equity'], Revenue: ['إيرادات', 'Revenue'],
    CostOfRevenue: ['تكلفة الإيرادات', 'Cost of revenue'], Expenses: ['مصروفات', 'Expenses']
  } satisfies Labels<AccountType>,
  NormalBalance: { Debit: ['مدين', 'Debit'], Credit: ['دائن', 'Credit'] } satisfies Labels<NormalBalance>,
  FiscalYearStatus: { Open: ['مفتوحة', 'Open'], Closed: ['مقفلة', 'Closed'] } satisfies Labels<FiscalYearStatus>,
  AccountingPeriodStatus: { Open: ['مفتوحة', 'Open'], Closed: ['مقفلة', 'Closed'] } satisfies Labels<AccountingPeriodStatus>,
  PostingEventStatus: { Posted: ['مُرحّل', 'Posted'], Skipped: ['متجاوز', 'Skipped'], Duplicate: ['مكرر', 'Duplicate'], Failed: ['فشل', 'Failed'] } satisfies Labels<PostingEventStatus>,
  CustomerType: { Individual: ['فرد', 'Individual'], Business: ['شركة', 'Business'] } satisfies Labels<CustomerType>,
  CustomerRole: { Sender: ['مرسل', 'Sender'], Recipient: ['مستلم', 'Recipient'], Both: ['مرسل ومستلم', 'Both'] } satisfies Labels<CustomerRole>,
  VehicleType: { Motorcycle: ['دراجة نارية', 'Motorcycle'], Car: ['سيارة', 'Car'], Van: ['فان', 'Van'], Truck: ['شاحنة', 'Truck'], Other: ['أخرى', 'Other'] } satisfies Labels<VehicleType>,
  PaymentMethodKind: {
    Cash: ['نقداً', 'Cash'], CashOnDelivery: ['عند الاستلام', 'Cash on delivery'], BankTransfer: ['تحويل بنكي', 'Bank transfer'], Card: ['بطاقة', 'Card'],
    CustomerAccount: ['على الحساب', 'Customer account'], Wallet: ['محفظة', 'Wallet']
  } satisfies Labels<PaymentMethodKind>,
  AuditAction: {
    Login: ['تسجيل دخول', 'Login'], LoginFailed: ['فشل دخول', 'Login failed'], RefreshTokenRevoked: ['إلغاء جلسة', 'Session revoked'],
    RefreshTokenReuseDetected: ['محاولة إعادة استخدام جلسة', 'Refresh token reuse'], PasswordChanged: ['تغيير كلمة المرور', 'Password changed'],
    UserCreated: ['إنشاء مستخدم', 'User created'], UserUpdated: ['تعديل مستخدم', 'User updated'], UserActivated: ['تفعيل مستخدم', 'User activated'],
    UserDeactivated: ['إيقاف مستخدم', 'User deactivated'], RoleCreated: ['إنشاء دور', 'Role created'], RolePermissionsChanged: ['تعديل صلاحيات دور', 'Role permissions changed'],
    MasterDataCreated: ['إضافة بيانات أساسية', 'Master data created'], MasterDataUpdated: ['تعديل بيانات أساسية', 'Master data updated'],
    MasterDataActivated: ['تفعيل بيانات أساسية', 'Master data activated'], MasterDataDeactivated: ['إيقاف بيانات أساسية', 'Master data deactivated'],
    MasterDataDeleted: ['حذف بيانات أساسية', 'Master data deleted'], ShipmentCreated: ['تسجيل شحنة', 'Shipment created'], ShipmentUpdated: ['تعديل شحنة', 'Shipment updated'],
    ShipmentCancelled: ['إلغاء شحنة', 'Shipment cancelled'], ShipmentStatusCorrected: ['تصحيح حالة شحنة', 'Status corrected'], ShipmentsImported: ['استيراد شحنات', 'Shipments imported'],
    ShipmentAttachmentUploaded: ['رفع مرفق', 'Attachment uploaded'], ShipmentAttachmentDeleted: ['حذف مرفق', 'Attachment deleted'],
    ShipmentDriverAssigned: ['إسناد سائق', 'Driver assigned'], ShippingSettingsChanged: ['تعديل إعدادات الشحن', 'Shipping settings changed'],
    ShipmentRepriced: ['إعادة تسعير', 'Shipment repriced'], PriceOverrideRequested: ['طلب سعر يدوي', 'Price override requested'],
    PriceOverrideApproved: ['اعتماد سعر يدوي', 'Price override approved'], PriceOverrideRejected: ['رفض سعر يدوي', 'Price override rejected'],
    InvoiceCreated: ['إنشاء فاتورة', 'Invoice created'], InvoiceUpdated: ['تعديل فاتورة', 'Invoice updated'], InvoiceIssued: ['إصدار فاتورة', 'Invoice issued'],
    InvoiceCancelled: ['إلغاء فاتورة', 'Invoice cancelled'], CreditNoteIssued: ['إشعار دائن', 'Credit note issued'],
    CustomerPaymentRecorded: ['تسجيل دفعة عميل', 'Customer payment recorded'], CustomerPaymentAllocated: ['توزيع دفعة', 'Payment allocated'],
    CodCollected: ['تحصيل نقدي', 'COD collected'], DriverCashHandedOver: ['تسليم عهدة سائق', 'Driver cash handed over'], CodMarkedReady: ['تجهيز للتسوية', 'COD marked ready'],
    CodDisputeOpened: ['فتح نزاع تحصيل', 'COD dispute opened'], CodDisputeResolved: ['حل نزاع تحصيل', 'COD dispute resolved'],
    CodAdjustmentRequested: ['طلب تسوية تحصيل', 'COD adjustment requested'], CodAdjustmentApproved: ['اعتماد تسوية تحصيل', 'COD adjustment approved'],
    CodAdjustmentRejected: ['رفض تسوية تحصيل', 'COD adjustment rejected'], SenderSettlementCreated: ['إنشاء تسوية مرسل', 'Settlement created'],
    SenderSettlementApproved: ['اعتماد تسوية مرسل', 'Settlement approved'], SenderSettlementPaid: ['دفع تسوية مرسل', 'Settlement paid'],
    SenderSettlementCancelled: ['إلغاء تسوية مرسل', 'Settlement cancelled'], SenderSettlementReversed: ['عكس تسوية مرسل', 'Settlement reversed'],
    CashboxCreated: ['إنشاء صندوق', 'Cashbox created'], CashboxUpdated: ['تعديل صندوق', 'Cashbox updated'], CashReceiptPosted: ['سند قبض', 'Cash receipt posted'],
    CashPaymentPosted: ['سند صرف', 'Cash payment posted'], CashVoucherCancelled: ['إلغاء سند', 'Voucher cancelled'],
    CashTransferRequested: ['طلب تحويل نقدي', 'Cash transfer requested'], CashTransferApproved: ['اعتماد تحويل نقدي', 'Cash transfer approved'],
    CashTransferRejected: ['رفض تحويل نقدي', 'Cash transfer rejected'], CashTransferReceived: ['استلام تحويل نقدي', 'Cash transfer received'],
    CashTransferCancelled: ['إلغاء تحويل نقدي', 'Cash transfer cancelled'], CashboxDayClosed: ['إقفال يومي', 'Cashbox day closed'],
    CashboxClosingApproved: ['اعتماد إقفال', 'Closing approved'], CashboxClosingRejected: ['رفض إقفال', 'Closing rejected'],
    BankAccountSaved: ['حفظ حساب بنكي', 'Bank account saved'], AccountSaved: ['حفظ حساب', 'Account saved'], AccountDeleted: ['حذف حساب', 'Account deleted'],
    AccountCategorySaved: ['حفظ تصنيف حساب', 'Account category saved'], AccountingDimensionSaved: ['حفظ بُعد محاسبي', 'Dimension saved'],
    FiscalYearCreated: ['إنشاء سنة مالية', 'Fiscal year created'], FiscalYearUpdated: ['تعديل سنة مالية', 'Fiscal year updated'],
    FiscalYearDeleted: ['حذف سنة مالية', 'Fiscal year deleted'], FiscalYearClosed: ['إقفال سنة مالية', 'Fiscal year closed'],
    AccountingPeriodClosed: ['إقفال فترة', 'Period closed'], AccountingPeriodReopened: ['إعادة فتح فترة', 'Period reopened'],
    JournalEntryCreated: ['إنشاء قيد', 'Journal created'], JournalEntryUpdated: ['تعديل قيد', 'Journal updated'], JournalEntryDeleted: ['حذف قيد', 'Journal deleted'],
    JournalEntrySubmitted: ['تقديم قيد', 'Journal submitted'], JournalEntryApproved: ['اعتماد قيد', 'Journal approved'], JournalEntryRejected: ['رفض قيد', 'Journal rejected'],
    JournalEntryPosted: ['ترحيل قيد', 'Journal posted'], JournalEntryReversed: ['عكس قيد', 'Journal reversed'], PostingRuleSaved: ['حفظ قاعدة ترحيل', 'Posting rule saved'],
    PostingRuleDeleted: ['حذف قاعدة ترحيل', 'Posting rule deleted'], PostingEventRetried: ['إعادة محاولة ترحيل', 'Posting retried'],
    BankStatementImported: ['استيراد كشف بنك', 'Bank statement imported'], BankReconciliationSaved: ['حفظ مطابقة بنكية', 'Reconciliation saved'],
    BankReconciliationCompleted: ['إكمال مطابقة بنكية', 'Reconciliation completed'], AccountingPeriodUpdated: ['تعديل فترة', 'Period updated']
  } satisfies Labels<AuditAction>
} as const;

export type EnumName = keyof typeof enumLabels;

export function enumLabel(name: EnumName, value: string | null | undefined, language: Language = 'ar'): string {
  if (!value) {
    return '-';
  }

  const pair = (enumLabels[name] as Record<string, readonly [string, string]>)[value];
  return pair ? pair[language === 'en' ? 1 : 0] : value;
}

/** Options for a select: [{ value, label }] in the declaration order. */
export function enumOptions(name: EnumName, language: Language = 'ar'): { value: string; label: string }[] {
  return Object.entries(enumLabels[name] as Record<string, readonly [string, string]>).map(([value, pair]) => ({
    value,
    label: pair[language === 'en' ? 1 : 0]
  }));
}
