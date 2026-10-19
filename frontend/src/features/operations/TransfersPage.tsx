import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { BranchTransferDto } from '@/api/generated';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { splitCodes } from '@/components/form/ScanBox';
import { useTx } from '@/i18n';

type Row = BranchTransferDto & GridValidRowModel;

/** Branch transfers (manifest between branches): Draft -> Dispatched -> Received / PartiallyReceived. */
export default function TransfersPage() {
  const { t } = useTranslation();
  const tx = useTx();
  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.transfers'),
        url: '/branch-transfers',
        managePermission: P.Transfers.Manage,
        defaultSort: 'createdAtUtc',
        rowLink: (row) => `/transfers/${row.id}`,
        createLabel: tx('تحويل جديد', 'New transfer'),
        columns: [
          col.text('transferNumber', tx('الرقم', 'Number'), { minWidth: 140 }),
          col.status('status', t('common.status'), 'BranchTransferStatus'),
          col.text('fromBranchName', tx('من فرع', 'From')),
          col.text('toBranchName', tx('إلى فرع', 'To')),
          col.text('driverName', t('common.driver')),
          col.text('vehiclePlateNumber', tx('المركبة', 'Vehicle')),
          col.number('itemCount', tx('الشحنات', 'Shipments')),
          col.number('receivedCount', tx('المستلمة', 'Received')),
          col.number('missingCount', tx('المفقودة', 'Missing')),
          col.dateTime('createdAtUtc', tx('أُنشئ', 'Created'), S)
        ],
        filters: [
          { name: 'status', label: t('common.status'), type: 'enum', enumName: 'BranchTransferStatus' },
          { name: 'fromBranchId', label: tx('من فرع', 'From'), type: 'lookup', lookup: 'branches' },
          { name: 'toBranchId', label: tx('إلى فرع', 'To'), type: 'lookup', lookup: 'branches' }
        ],
        form: {
          fields: [
            { name: 'fromBranchId', label: tx('من فرع', 'From branch'), type: 'lookup', lookup: 'branches', required: true },
            { name: 'toBranchId', label: tx('إلى فرع', 'To branch'), type: 'lookup', lookup: 'branches', required: true },
            { name: 'driverId', label: t('common.driver'), type: 'lookup', lookup: 'drivers' },
            { name: 'vehicleId', label: tx('المركبة', 'Vehicle'), type: 'lookup', lookup: 'vehicles' },
            { name: 'sealNumber', label: tx('رقم الختم', 'Seal number') },
            { name: 'codesText', label: tx('أرقام التتبع (سطر لكل رقم، اختياري)', 'Tracking numbers (one per line, optional)'), type: 'textarea' },
            { name: 'notes', label: t('common.notes'), type: 'textarea' }
          ],
          toRequest: ({ codesText, ...values }) => ({ ...values, codes: splitCodes(codesText as string) }),
          canEdit: () => false
        }
      }}
    />
  );
}
