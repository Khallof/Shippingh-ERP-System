import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { CashboxDto } from '@/api/generated';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';

type Row = CashboxDto & GridValidRowModel;

/** Cashboxes (one currency each). The balance changes only through vouchers, handovers and transfers. */
export default function CashboxesPage() {
  const { t } = useTranslation();
  const tx = useTx();
  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.cashboxes'),
        url: '/cashboxes',
        managePermission: P.Cashboxes.Manage,
        defaultSort: 'code',
        defaultDirection: 'asc',
        rowLink: (row) => `/cash/cashboxes/${row.id}`,
        columns: [
          col.text('code', tx('الرمز', 'Code'), { ...S, flex: 0, width: 110 }),
          col.text('arabicName', tx('الاسم', 'Name'), S),
          col.text('branchName', t('common.branch')),
          col.text('currencyCode', t('common.currency'), { flex: 0, width: 80 }),
          col.text('custodianName', tx('أمين الصندوق', 'Custodian')),
          col.money('currentBalance', tx('الرصيد', 'Balance'), 'currencyCode', S),
          col.dateTime('lastTransactionAtUtc', tx('آخر حركة', 'Last movement')),
          col.active('isActive', t('common.status'))
        ],
        filters: [
          { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
          { name: 'currencyId', label: t('common.currency'), type: 'lookup', lookup: 'currencies' },
          { name: 'isActive', label: t('common.active'), type: 'boolean' }
        ],
        form: {
          fields: [
            { name: 'code', label: tx('الرمز', 'Code'), required: true, createOnly: true, span: 3 },
            { name: 'arabicName', label: tx('الاسم بالعربية', 'Arabic name'), required: true, span: 5 },
            { name: 'englishName', label: tx('الاسم بالإنجليزية', 'English name'), span: 4 },
            { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches', required: true, createOnly: true },
            { name: 'currencyId', label: t('common.currency'), type: 'lookup', lookup: 'currencies', required: true, createOnly: true },
            { name: 'custodianUserId', label: tx('أمين الصندوق', 'Custodian'), type: 'lookup', lookup: 'users' },
            { name: 'openingBalance', label: tx('الرصيد الافتتاحي', 'Opening balance'), type: 'money', min: 0, createOnly: true },
            { name: 'isActive', label: t('common.active'), type: 'checkbox', hidden: (v) => v.id === undefined || v.id === null },
            { name: 'notes', label: t('common.notes'), type: 'textarea' }
          ],
          toValues: (row) => row
            ? { id: row.id, code: row.code, arabicName: row.arabicName, englishName: row.englishName, branchId: row.branchId, currencyId: row.currencyId, custodianUserId: row.custodianUserId, openingBalance: 0, isActive: row.isActive, notes: row.notes }
            : { code: '', arabicName: '', englishName: null, branchId: null, currencyId: null, custodianUserId: null, openingBalance: 0, notes: null },
          toRequest: (values, row) => row
            ? { arabicName: values.arabicName, englishName: values.englishName, custodianUserId: values.custodianUserId, isActive: values.isActive, notes: values.notes }
            : values
        }
      }}
    />
  );
}
