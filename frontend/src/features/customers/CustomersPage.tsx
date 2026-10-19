import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { CustomerDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';
import { customerFields, newCustomerValues } from './customerFields';

type Row = CustomerDto & GridValidRowModel;

export default function CustomersPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const { user } = useAuth();
  const fields = customerFields(tx);

  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.customers'),
        url: '/customers',
        managePermission: P.Customers.Manage,
        canActivate: true,
        canDelete: true,
        defaultSort: 'createdAtUtc',
        searchPlaceholder: tx('الرمز، الاسم أو الهاتف', 'Code, name or phone'),
        rowLink: (row) => `/customers/${row.id}`,
        createLabel: tx('عميل جديد', 'New customer'),
        columns: [
          col.text('code', tx('الرمز', 'Code'), { ...S, flex: 0, width: 110 }),
          col.text('arabicName', tx('الاسم', 'Name'), { ...S, minWidth: 180 }),
          col.enumText('customerType', tx('النوع', 'Type'), 'CustomerType'),
          col.enumText('roles', tx('الدور', 'Role'), 'CustomerRole'),
          col.text('phone', tx('الهاتف', 'Phone'), { renderCell: ({ value }) => <span dir="ltr">{value}</span> }),
          col.text('defaultBranchName', tx('الفرع', 'Branch')),
          col.text('pricingPlanName', tx('خطة التسعير', 'Pricing plan')),
          col.money('creditLimit', tx('حد الائتمان', 'Credit limit'), undefined, S),
          col.number('addressCount', tx('العناوين', 'Addresses')),
          col.active('isActive', t('common.status'))
        ],
        filters: [
          { name: 'customerType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'CustomerType' },
          { name: 'role', label: tx('الدور', 'Role'), type: 'enum', enumName: 'CustomerRole' },
          { name: 'defaultBranchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
          { name: 'isActive', label: t('common.active'), type: 'boolean' }
        ],
        form: {
          fields,
          toValues: (row) => (row ? Object.fromEntries(fields.map((f) => [f.name, (row as Record<string, unknown>)[f.name] ?? null])) : newCustomerValues('Both', user?.branchId ?? null))
        }
      }}
    />
  );
}
