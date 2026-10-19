import type { FieldDef } from '@/components/form/fields';

type Tx = (arabic: string, english: string) => string;

/** Customer form (create / edit, and quick-add from the new shipment screen). */
export function customerFields(tx: Tx): FieldDef[] {
  return [
    { name: 'arabicName', label: tx('الاسم بالعربية', 'Arabic name'), required: true, maxLength: 200 },
    { name: 'englishName', label: tx('الاسم بالإنجليزية', 'English name'), maxLength: 200 },
    { name: 'customerType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'CustomerType', required: true, span: 3 },
    { name: 'roles', label: tx('الدور', 'Role'), type: 'enum', enumName: 'CustomerRole', required: true, span: 3 },
    { name: 'phone', label: tx('الهاتف', 'Phone'), required: true, maxLength: 30, span: 3 },
    { name: 'alternatePhone', label: tx('هاتف بديل', 'Alternate phone'), maxLength: 30, span: 3 },
    { name: 'email', label: tx('البريد', 'E-mail'), type: 'email', maxLength: 256 },
    { name: 'nationalOrCompanyNumber', label: tx('الرقم الوطني / السجل', 'National / company no.'), maxLength: 50 },
    { name: 'defaultBranchId', label: tx('الفرع الافتراضي', 'Default branch'), type: 'lookup', lookup: 'branches', required: true },
    { name: 'pricingPlanId', label: tx('خطة التسعير', 'Pricing plan'), type: 'lookup', lookup: 'pricingPlans' },
    { name: 'creditLimit', label: tx('حد الائتمان', 'Credit limit'), type: 'money', min: 0, span: 3 },
    { name: 'openingBalance', label: tx('الرصيد الافتتاحي', 'Opening balance'), type: 'money', span: 3 },
    { name: 'code', label: tx('الرمز (تلقائي إن تُرك فارغاً)', 'Code (auto when empty)'), maxLength: 20, createOnly: true },
    { name: 'notes', label: tx('ملاحظات', 'Notes'), type: 'textarea', maxLength: 1000 }
  ];
}

export const newCustomerValues = (roles: 'Sender' | 'Recipient' | 'Both' = 'Both', defaultBranchId: number | null = null) => ({
  arabicName: '',
  englishName: null,
  customerType: 'Individual',
  roles,
  phone: '',
  alternatePhone: null,
  email: null,
  nationalOrCompanyNumber: null,
  defaultBranchId,
  pricingPlanId: null,
  creditLimit: 0,
  openingBalance: 0,
  code: null,
  notes: null
});
