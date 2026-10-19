// Arabic (default) dictionary. en.ts must have exactly the same keys (checked by TypeScript).
const ar = {
  app: { name: 'نظام الشحن', company: 'شركة الشحن السريع' },
  common: {
    save: 'حفظ', cancel: 'إلغاء', close: 'إغلاق', delete: 'حذف', edit: 'تعديل', add: 'إضافة', create: 'إنشاء',
    search: 'بحث', filter: 'تصفية', reset: 'إعادة ضبط', refresh: 'تحديث', confirm: 'تأكيد', back: 'رجوع',
    yes: 'نعم', no: 'لا', all: 'الكل', actions: 'إجراءات', details: 'التفاصيل', print: 'طباعة',
    exportExcel: 'تصدير Excel', exportPdf: 'تصدير PDF', loading: 'جارٍ التحميل…', noData: 'لا توجد بيانات',
    active: 'فعّال', inactive: 'غير فعّال', activate: 'تفعيل', deactivate: 'إيقاف', status: 'الحالة',
    fromDate: 'من تاريخ', toDate: 'إلى تاريخ', branch: 'الفرع', customer: 'العميل', driver: 'السائق',
    currency: 'العملة', amount: 'المبلغ', date: 'التاريخ', notes: 'ملاحظات', reason: 'السبب', code: 'الرمز',
    arabicName: 'الاسم بالعربية', englishName: 'الاسم بالإنجليزية', total: 'الإجمالي', saved: 'تم الحفظ',
    done: 'تمت العملية', required: 'هذا الحقل مطلوب', retry: 'إعادة المحاولة', language: 'اللغة',
    darkMode: 'الوضع الداكن', open: 'فتح', select: 'اختر', view: 'عرض', submit: 'تقديم', approve: 'اعتماد',
    reject: 'رفض', post: 'ترحيل', reverse: 'عكس', pay: 'دفع', receive: 'استلام', dispatch: 'إرسال',
    perPage: 'لكل صفحة', of: 'من', traceId: 'رقم التتبع الفني'
  },
  auth: {
    title: 'تسجيل الدخول', userName: 'اسم المستخدم أو البريد', password: 'كلمة المرور', signIn: 'دخول',
    signOut: 'تسجيل الخروج', profile: 'الملف الشخصي', changePassword: 'تغيير كلمة المرور',
    currentPassword: 'كلمة المرور الحالية', newPassword: 'كلمة المرور الجديدة', confirmPassword: 'تأكيد كلمة المرور',
    passwordChanged: 'تم تغيير كلمة المرور، وتم تسجيل الخروج من الأجهزة الأخرى.', sessionExpired: 'انتهت الجلسة، سجّل الدخول من جديد.',
    forbiddenTitle: 'لا تملك صلاحية', forbiddenText: 'هذه الصفحة تحتاج صلاحية غير ممنوحة لحسابك. تواصل مع مدير النظام.',
    roles: 'الأدوار', permissions: 'الصلاحيات', lastLogin: 'آخر دخول'
  },
  nav: {
    dashboard: 'لوحة المعلومات', operations: 'العمليات', shipments: 'الشحنات', newShipment: 'شحنة جديدة',
    transfers: 'التحويلات بين الفروع', manifests: 'كشوف التوزيع', driver: 'شاشة السائق', returns: 'المرتجعات',
    customers: 'العملاء', masterData: 'البيانات الأساسية', pricing: 'التسعير', pricingPlans: 'خطط وقواعد التسعير',
    priceOverrides: 'اعتماد الأسعار اليدوية', billing: 'الفوترة', invoices: 'الفواتير', payments: 'دفعات العملاء',
    cod: 'الدفع عند الاستلام', codDashboard: 'لوحة التحصيل', driverBalances: 'عهد السائقين', settlements: 'تسويات المرسلين',
    cash: 'النقدية', cashboxes: 'الصناديق', receipts: 'سندات القبض', cashPayments: 'سندات الصرف', cashTransfers: 'التحويلات النقدية',
    expenses: 'المصروفات', accounting: 'المحاسبة', chartOfAccounts: 'دليل الحسابات', journalEntries: 'قيود اليومية',
    fiscalPeriods: 'السنوات والفترات', postingRules: 'الترحيل الآلي', reports: 'التقارير', financialReports: 'التقارير المالية',
    operationalReports: 'التقارير التشغيلية', admin: 'الإدارة', users: 'المستخدمون', roles: 'الأدوار والصلاحيات',
    auditLogs: 'سجل التدقيق', settings: 'الإعدادات وملف الشركة'
  },
  dashboard: {
    title: 'لوحة المعلومات', todayShipments: 'شحنات اليوم', delivered: 'المُسلّمة اليوم', failedToday: 'الفاشلة اليوم',
    openShipments: 'شحنات قيد التنفيذ', outForDelivery: 'خارج للتوزيع', deliveredPercent: 'نسبة التسليم',
    failedPercent: 'نسبة الإخفاق', byStatus: 'الشحنات حسب الحالة', last7Days: 'آخر 7 أيام', created: 'أُنشئت',
    codPending: 'تحصيل لم يتم بعد', codWithDrivers: 'مع السائقين', codReady: 'جاهز لتسوية المرسلين',
    cashboxes: 'أرصدة الصناديق', revenue: 'الإيرادات (هذا الشهر)', expenses: 'المصروفات (هذا الشهر)',
    netProfit: 'صافي الربح التقديري', overdueInvoices: 'فواتير متأخرة', driverBalances: 'أعلى عهد السائقين',
    topCustomers: 'أكثر العملاء شحناً', recentActivity: 'آخر الحركات', period: 'الفترة (أيام)', receivables: 'ذمم العملاء المفتوحة',
    shipmentsCount: 'الشحنات', charges: 'الأجور'
  },
  errors: {
    notFoundTitle: 'الصفحة غير موجودة', notFoundText: 'الرابط غير صحيح أو تم نقل الصفحة.', goHome: 'العودة للرئيسية',
    unexpected: 'حدث خطأ غير متوقع'
  }
};

export default ar;
export type Dictionary = typeof ar;
