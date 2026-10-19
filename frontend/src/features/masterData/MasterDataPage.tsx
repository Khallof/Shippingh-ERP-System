import { useMemo } from 'react';
import { Navigate, useNavigate, useParams } from 'react-router-dom';
import { Card, Tab, Tabs } from '@mui/material';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import ResourcePage, { type ResourceConfig } from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import type { FieldDef } from '@/components/form/fields';
import { useTx } from '@/i18n';

type Tx = (ar: string, en: string) => string;
type Row = GridValidRowModel & { id: number };

interface TabDef {
  key: string;
  label: string;
  view?: string[];
  config: ResourceConfig<Row>;
}

const names = (tx: Tx): FieldDef[] => [
  { name: 'code', label: tx('الرمز', 'Code'), required: true, maxLength: 20, span: 3 },
  { name: 'arabicName', label: tx('الاسم بالعربية', 'Arabic name'), required: true, maxLength: 200, span: 5 },
  { name: 'englishName', label: tx('الاسم بالإنجليزية', 'English name'), maxLength: 200, span: 4 }
];

const nameColumns = (tx: Tx) => [
  col.text<Row>('code', tx('الرمز', 'Code'), { ...S, flex: 0, width: 110 }),
  col.text<Row>('arabicName', tx('الاسم', 'Name'), S),
  col.text<Row>('englishName', tx('الاسم بالإنجليزية', 'English name'))
];

/** Master data screens (branches, warehouses, geography, services, currencies, people, vehicles...). */
function tabs(tx: Tx): TabDef[] {
  const active = col.active<Row>('isActive', tx('الحالة', 'Status'));
  const base = { canActivate: true, canDelete: true, searchPlaceholder: tx('الرمز أو الاسم', 'Code or name') };
  return [
    {
      key: 'branches', label: tx('الفروع', 'Branches'),
      config: { ...base, title: tx('الفروع', 'Branches'), url: '/branches', managePermission: P.Branches.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('cityName', tx('المدينة', 'City')), col.text('phone', tx('الهاتف', 'Phone')), col.bool('isHeadOffice', tx('رئيسي', 'Head office')), col.number('warehouseCount', tx('المستودعات', 'Warehouses')), active],
        filters: [{ name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities' }, { name: 'isActive', label: tx('فعّال', 'Active'), type: 'boolean' }],
        form: { fields: [...names(tx), { name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities', required: true }, { name: 'phone', label: tx('الهاتف', 'Phone'), span: 3 }, { name: 'email', label: tx('البريد', 'E-mail'), type: 'email', span: 3 }, { name: 'address', label: tx('العنوان', 'Address'), span: 12 }, { name: 'isHeadOffice', label: tx('المركز الرئيسي', 'Head office'), type: 'checkbox' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'warehouses', label: tx('المستودعات', 'Warehouses'),
      config: { ...base, title: tx('المستودعات', 'Warehouses'), url: '/warehouses', managePermission: P.Branches.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('branchName', tx('الفرع', 'Branch')), col.number('capacityCubicMeters', tx('السعة م³', 'Capacity m³'), 2), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }],
        form: { fields: [...names(tx), { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches', required: true }, { name: 'capacityCubicMeters', label: tx('السعة م³', 'Capacity m³'), type: 'number', min: 0, span: 3 }, { name: 'phone', label: tx('الهاتف', 'Phone'), span: 3 }, { name: 'address', label: tx('العنوان', 'Address'), span: 12 }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'countries', label: tx('الدول', 'Countries'),
      config: { ...base, title: tx('الدول', 'Countries'), url: '/countries', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('phoneCode', tx('رمز الهاتف', 'Phone code')), col.number('cityCount', tx('المدن', 'Cities')), active],
        form: { fields: [...names(tx), { name: 'phoneCode', label: tx('رمز الهاتف', 'Phone code'), maxLength: 10 }] } }
    },
    {
      key: 'cities', label: tx('المدن', 'Cities'),
      config: { ...base, title: tx('المدن', 'Cities'), url: '/cities', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('countryName', tx('الدولة', 'Country')), col.number('zoneCount', tx('المناطق', 'Zones')), active],
        filters: [{ name: 'countryId', label: tx('الدولة', 'Country'), type: 'lookup', lookup: 'countries' }],
        form: { fields: [{ name: 'countryId', label: tx('الدولة', 'Country'), type: 'lookup', lookup: 'countries', required: true, span: 12 }, ...names(tx)] } }
    },
    {
      key: 'zones', label: tx('المناطق', 'Zones'),
      config: { ...base, title: tx('المناطق', 'Zones'), url: '/zones', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('cityName', tx('المدينة', 'City')), col.bool('isRemoteArea', tx('نائية', 'Remote')), active],
        filters: [{ name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities' }, { name: 'isRemoteArea', label: tx('نائية', 'Remote'), type: 'boolean' }],
        form: { fields: [{ name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities', required: true, span: 12 }, ...names(tx), { name: 'isRemoteArea', label: tx('منطقة نائية (رسوم إضافية)', 'Remote area (extra fee)'), type: 'checkbox' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'services', label: tx('أنواع البضاعة', 'Goods types'),
      config: { ...base, title: tx('أنواع البضاعة (تُستخدم في قوائم الأسعار)', 'Goods types (used by the price lists)'), url: '/service-types', managePermission: P.MasterData.Manage, defaultSort: 'sortOrder', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.number('estimatedDeliveryDays', tx('أيام التوصيل', 'Delivery days')), col.number('sortOrder', tx('الترتيب', 'Order'), 0, S), active],
        form: { fields: [...names(tx), { name: 'estimatedDeliveryDays', label: tx('أيام التوصيل المتوقعة', 'Estimated days'), type: 'integer', required: true, min: 0, span: 3 }, { name: 'sortOrder', label: tx('الترتيب', 'Order'), type: 'integer', span: 3 }, { name: 'description', label: tx('الوصف', 'Description'), type: 'textarea' }] } }
    },
    {
      key: 'currencies', label: tx('العملات', 'Currencies'),
      config: { ...base, title: tx('العملات', 'Currencies'), url: '/currencies', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('symbol', tx('الرمز المختصر', 'Symbol')), col.number('decimalPlaces', tx('الكسور', 'Decimals')), col.bool('isBaseCurrency', tx('أساسية', 'Base')), active],
        form: { fields: [...names(tx), { name: 'symbol', label: tx('الرمز المختصر', 'Symbol'), required: true, span: 3 }, { name: 'decimalPlaces', label: tx('عدد الكسور', 'Decimals'), type: 'integer', min: 0, max: 4, span: 3 }, { name: 'isBaseCurrency', label: tx('العملة الأساسية', 'Base currency'), type: 'checkbox' }] } }
    },
    {
      key: 'rates', label: tx('أسعار الصرف', 'Exchange rates'),
      config: { ...base, title: tx('أسعار الصرف', 'Exchange rates'), url: '/exchange-rates', managePermission: P.MasterData.Manage, defaultSort: 'effectiveFromUtc',
        columns: [col.text('fromCurrencyCode', tx('من', 'From')), col.text('toCurrencyCode', tx('إلى', 'To')), col.number('rate', tx('السعر', 'Rate'), 6, S), col.date('effectiveFromUtc', tx('ساري من', 'Effective from'), S), col.text('notes', tx('ملاحظات', 'Notes')), active],
        form: { fields: [{ name: 'fromCurrencyId', label: tx('من عملة', 'From currency'), type: 'lookup', lookup: 'currencies', required: true }, { name: 'toCurrencyId', label: tx('إلى عملة', 'To currency'), type: 'lookup', lookup: 'currencies', required: true }, { name: 'rate', label: tx('السعر', 'Rate'), type: 'number', required: true, min: 0 }, { name: 'effectiveFromUtc', label: tx('ساري من', 'Effective from'), type: 'date', required: true }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'payment-methods', label: tx('طرق الدفع', 'Payment methods'),
      config: { ...base, title: tx('طرق الدفع', 'Payment methods'), url: '/payment-methods', managePermission: P.MasterData.Manage, defaultSort: 'sortOrder', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.enumText('kind', tx('النوع', 'Kind'), 'PaymentMethodKind'), col.bool('requiresReference', tx('يتطلب مرجعاً', 'Needs reference')), active],
        form: { fields: [...names(tx), { name: 'kind', label: tx('النوع', 'Kind'), type: 'enum', enumName: 'PaymentMethodKind', required: true, span: 4 }, { name: 'sortOrder', label: tx('الترتيب', 'Order'), type: 'integer', span: 2 }, { name: 'requiresReference', label: tx('يتطلب رقماً مرجعياً', 'Requires a reference'), type: 'checkbox' }] } }
    },
    {
      key: 'tax-rates', label: tx('الضرائب', 'Tax rates'),
      config: { ...base, title: tx('الضرائب', 'Tax rates'), url: '/tax-rates', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.number('percentage', '%', 2, S), col.bool('isDefault', tx('افتراضية', 'Default')), active],
        form: { fields: [...names(tx), { name: 'percentage', label: tx('النسبة %', 'Percentage %'), type: 'number', required: true, min: 0, max: 100 }, { name: 'isDefault', label: tx('افتراضية', 'Default'), type: 'checkbox' }] } }
    },
    {
      key: 'expense-categories', label: tx('بنود المصروفات', 'Expense categories'),
      config: { ...base, title: tx('بنود المصروفات', 'Expense categories'), url: '/expense-categories', managePermission: P.MasterData.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx), col.text('description', tx('الوصف', 'Description')), active],
        form: { fields: [...names(tx), { name: 'description', label: tx('الوصف', 'Description'), type: 'textarea' }] } }
    },
    {
      key: 'employees', label: tx('الموظفون', 'Employees'), view: [P.Employees.View],
      config: { ...base, title: tx('الموظفون', 'Employees'), url: '/employees', managePermission: P.Employees.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx).slice(0, 2), col.text('branchName', tx('الفرع', 'Branch')), col.text('jobTitle', tx('الوظيفة', 'Job title')), col.text('phone', tx('الهاتف', 'Phone')), col.text('userName', tx('المستخدم', 'User')), col.bool('isDriver', tx('سائق', 'Driver')), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }],
        form: { fields: [...names(tx), { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches', required: true }, { name: 'jobTitle', label: tx('الوظيفة', 'Job title') }, { name: 'phone', label: tx('الهاتف', 'Phone'), span: 4 }, { name: 'email', label: tx('البريد', 'E-mail'), type: 'email', span: 4 }, { name: 'nationalId', label: tx('الرقم الوطني', 'National id'), span: 4 }, { name: 'hireDateUtc', label: tx('تاريخ التعيين', 'Hire date'), type: 'date' }, { name: 'userId', label: tx('حساب المستخدم', 'User account'), type: 'lookup', lookup: 'users' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'drivers', label: tx('السائقون', 'Drivers'), view: [P.Employees.View],
      config: { ...base, title: tx('السائقون', 'Drivers'), url: '/drivers', managePermission: P.Employees.Manage, defaultSort: 'employeeName', defaultDirection: 'asc',
        columns: [col.text('employeeCode', tx('الرمز', 'Code'), { flex: 0, width: 100 }), col.text('employeeName', tx('الاسم', 'Name'), S), col.text('branchName', tx('الفرع', 'Branch')), col.text('licenseNumber', tx('الرخصة', 'License')), col.date('licenseExpiryDateUtc', tx('انتهاء الرخصة', 'License expiry'), S), col.text('defaultVehiclePlate', tx('المركبة', 'Vehicle')), col.text('userName', tx('المستخدم', 'User')), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }, { name: 'licenseExpiringSoon', label: tx('رخصة تنتهي قريباً', 'License expiring'), type: 'boolean' }],
        form: { fields: [{ name: 'employeeId', label: tx('الموظف', 'Employee'), type: 'lookup', lookup: 'employees', required: true, createOnly: true }, { name: 'userId', label: tx('حساب المستخدم (لشاشة السائق)', 'User account (driver screen)'), type: 'lookup', lookup: 'users' }, { name: 'licenseNumber', label: tx('رقم الرخصة', 'License number'), required: true, span: 4 }, { name: 'licenseCategory', label: tx('الفئة', 'Category'), span: 4 }, { name: 'licenseExpiryDateUtc', label: tx('انتهاء الرخصة', 'Expiry'), type: 'date', required: true, span: 4 }, { name: 'defaultVehicleId', label: tx('المركبة الافتراضية', 'Default vehicle'), type: 'lookup', lookup: 'vehicles' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'vehicles', label: tx('المركبات', 'Vehicles'), view: [P.Vehicles.View],
      config: { ...base, title: tx('المركبات', 'Vehicles'), url: '/vehicles', managePermission: P.Vehicles.Manage, defaultSort: 'plateNumber', defaultDirection: 'asc',
        columns: [col.text('plateNumber', tx('اللوحة', 'Plate'), S), col.text('model', tx('الطراز', 'Model'), S), col.enumText('vehicleType', tx('النوع', 'Type'), 'VehicleType'), col.number('capacityKg', tx('الحمولة كغ', 'Capacity kg'), 0, S), col.text('branchName', tx('الفرع', 'Branch')), active],
        filters: [{ name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }, { name: 'vehicleType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'VehicleType' }],
        form: { fields: [{ name: 'plateNumber', label: tx('رقم اللوحة', 'Plate number'), required: true, span: 4 }, { name: 'model', label: tx('الطراز', 'Model'), required: true, span: 4 }, { name: 'vehicleType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'VehicleType', required: true, span: 4 }, { name: 'capacityKg', label: tx('الحمولة كغ', 'Capacity kg'), type: 'number', min: 0 }, { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches', required: true }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'suppliers', label: tx('الموردون', 'Suppliers'), view: [P.Suppliers.View],
      config: { ...base, title: tx('الموردون', 'Suppliers'), url: '/suppliers', managePermission: P.Suppliers.Manage, defaultSort: 'code', defaultDirection: 'asc',
        columns: [...nameColumns(tx).slice(0, 2), col.text('contactPerson', tx('جهة الاتصال', 'Contact')), col.text('phone', tx('الهاتف', 'Phone')), col.text('taxNumber', tx('الرقم الضريبي', 'Tax no.')), col.money('openingBalance', tx('الرصيد الافتتاحي', 'Opening balance')), active],
        form: { fields: [...names(tx), { name: 'contactPerson', label: tx('جهة الاتصال', 'Contact person') }, { name: 'phone', label: tx('الهاتف', 'Phone'), required: true, span: 3 }, { name: 'alternatePhone', label: tx('هاتف بديل', 'Alt. phone'), span: 3 }, { name: 'email', label: tx('البريد', 'E-mail'), type: 'email' }, { name: 'taxNumber', label: tx('الرقم الضريبي', 'Tax number') }, { name: 'openingBalance', label: tx('الرصيد الافتتاحي', 'Opening balance'), type: 'money' }, { name: 'address', label: tx('العنوان', 'Address'), span: 12 }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    },
    {
      key: 'bank-accounts', label: tx('الحسابات البنكية', 'Bank accounts'), view: [P.Cashboxes.View],
      config: { ...base, canActivate: false, title: tx('الحسابات البنكية', 'Bank accounts'), url: '/bank-accounts', managePermission: P.Cashboxes.Manage,
        columns: [col.text('code', tx('الرمز', 'Code'), { flex: 0, width: 100 }), col.text('arabicName', tx('الاسم', 'Name')), col.text('bankName', tx('البنك', 'Bank')), col.text('accountNumber', tx('رقم الحساب', 'Account no.')), col.text('currencyCode', tx('العملة', 'Currency'), { flex: 0, width: 80 }), col.text('accountName', tx('الحساب المحاسبي', 'GL account')), active],
        form: { fields: [{ name: 'code', label: tx('الرمز', 'Code'), required: true, span: 3 }, { name: 'arabicName', label: tx('الاسم', 'Name'), required: true, span: 5 }, { name: 'bankName', label: tx('البنك', 'Bank'), required: true, span: 4 }, { name: 'accountNumber', label: tx('رقم الحساب', 'Account number'), required: true }, { name: 'iban', label: 'IBAN' }, { name: 'currencyId', label: tx('العملة', 'Currency'), type: 'lookup', lookup: 'currencies', required: true }, { name: 'branchId', label: tx('الفرع', 'Branch'), type: 'lookup', lookup: 'branches' }, { name: 'accountId', label: tx('الحساب المحاسبي', 'GL account'), type: 'lookup', lookup: 'accounts' }, { name: 'isActive', label: tx('فعّال', 'Active'), type: 'checkbox' }, { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea' }] } }
    }
  ];
}

/** /master-data/:tab — one screen with a tab per master data list (tabs the user cannot view are hidden). */
export default function MasterDataPage() {
  const { tab } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const tx = useTx();
  const { hasAny } = useAuth();
  const visible = useMemo(() => tabs(tx).filter((item) => !item.view || hasAny(...item.view)), [tx, hasAny]);
  const current = visible.find((item) => item.key === tab);

  if (!current) {
    return <Navigate to={`/master-data/${visible[0].key}`} replace />;
  }

  return (
    <>
      <PageHeader title={t('nav.masterData')} />
      <Card sx={{ mb: 2 }}>
        <Tabs value={current.key} onChange={(_, value: string) => navigate(`/master-data/${value}`)} variant="scrollable" scrollButtons="auto">
          {visible.map((item) => <Tab key={item.key} value={item.key} label={item.label} />)}
        </Tabs>
      </Card>
      {/* key: a new list state per tab */}
      <ResourcePage key={current.key} config={{ ...current.config, hideHeader: false, height: 'calc(100vh - 340px)' }} />
    </>
  );
}
