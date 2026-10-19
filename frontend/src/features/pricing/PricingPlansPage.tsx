import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { PricingPlanDto } from '@/api/generated';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';

type Row = PricingPlanDto & GridValidRowModel;

/** Pricing plans (public default plan + contract plans per customer). A row opens its rules. */
export default function PricingPlansPage() {
  const { t } = useTranslation();
  const tx = useTx();
  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.pricingPlans'),
        url: '/pricing-plans',
        managePermission: P.Pricing.Manage,
        canActivate: true,
        canDelete: true,
        defaultSort: 'code',
        defaultDirection: 'asc',
        rowLink: (row) => `/pricing/${row.id}`,
        columns: [
          col.text('code', tx('الرمز', 'Code'), { ...S, flex: 0, width: 110 }),
          col.text('arabicName', tx('الاسم', 'Name'), S),
          col.text('currencyCode', tx('العملة', 'Currency'), { flex: 0, width: 80 }),
          col.bool('isDefault', tx('افتراضية', 'Default')),
          col.text('contractCustomerName', tx('عميل العقد', 'Contract customer')),
          col.date('validFromUtc', tx('من', 'From')),
          col.date('validToUtc', tx('إلى', 'To')),
          col.number('ruleCount', tx('القواعد', 'Rules')),
          col.number('customerCount', tx('العملاء', 'Customers')),
          col.active('isActive', t('common.status'))
        ],
        filters: [
          { name: 'isContract', label: tx('عقد', 'Contract'), type: 'boolean' },
          { name: 'isActive', label: t('common.active'), type: 'boolean' }
        ],
        form: {
          fields: [
            { name: 'code', label: tx('الرمز', 'Code'), required: true, span: 3 },
            { name: 'arabicName', label: tx('الاسم بالعربية', 'Arabic name'), required: true, span: 5 },
            { name: 'englishName', label: tx('الاسم بالإنجليزية', 'English name'), span: 4 },
            { name: 'currencyId', label: tx('العملة', 'Currency'), type: 'lookup', lookup: 'currencies', required: true },
            { name: 'isDefault', label: tx('الخطة الافتراضية', 'Default plan'), type: 'checkbox' },
            { name: 'contractCustomerId', label: tx('عميل العقد', 'Contract customer'), type: 'lookup', lookup: 'customers' },
            { name: 'contractReference', label: tx('مرجع العقد', 'Contract reference') },
            { name: 'validFromUtc', label: tx('ساري من', 'Valid from'), type: 'date' },
            { name: 'validToUtc', label: tx('ساري إلى', 'Valid to'), type: 'date' },
            { name: 'notes', label: t('common.notes'), type: 'textarea' }
          ]
        }
      }}
    />
  );
}
