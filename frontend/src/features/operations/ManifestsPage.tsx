import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { DriverManifestDto } from '@/api/generated';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { splitCodes } from '@/components/form/ScanBox';
import { useTx } from '@/i18n';

type Row = DriverManifestDto & GridValidRowModel;

/** Delivery manifests (driver run sheets): Draft -> Dispatched (shipments OutForDelivery) -> Closed. */
export default function ManifestsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.manifests'),
        url: '/driver-manifests',
        managePermission: P.DeliveryManifests.Manage,
        defaultSort: 'createdAtUtc',
        rowLink: (row) => `/manifests/${row.id}`,
        createLabel: tx('كشف جديد', 'New manifest'),
        columns: [
          col.text('manifestNumber', tx('الرقم', 'Number'), { minWidth: 140 }),
          col.status('status', t('common.status'), 'DriverManifestStatus'),
          col.date('manifestDateUtc', t('common.date')),
          col.text('driverName', t('common.driver')),
          col.text('branchName', t('common.branch')),
          col.number('itemCount', tx('الشحنات', 'Shipments')),
          col.number('deliveredCount', tx('سُلّمت', 'Delivered')),
          col.number('failedCount', tx('فشلت', 'Failed')),
          col.number('pendingCount', tx('متبقية', 'Pending')),
          col.money('totalCodAmount', tx('التحصيل', 'COD')),
          col.dateTime('createdAtUtc', tx('أُنشئ', 'Created'), S)
        ],
        filters: [
          { name: 'status', label: t('common.status'), type: 'enum', enumName: 'DriverManifestStatus' },
          { name: 'driverId', label: t('common.driver'), type: 'lookup', lookup: 'drivers' },
          { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' }
        ],
        form: {
          fields: [
            { name: 'driverId', label: t('common.driver'), type: 'lookup', lookup: 'drivers', required: true },
            { name: 'vehicleId', label: tx('المركبة', 'Vehicle'), type: 'lookup', lookup: 'vehicles' },
            { name: 'manifestDateUtc', label: t('common.date'), type: 'date' },
            { name: 'codesText', label: tx('أرقام التتبع (اختياري)', 'Tracking numbers (optional)'), type: 'textarea' },
            { name: 'notes', label: t('common.notes'), type: 'textarea' }
          ],
          toRequest: ({ codesText, ...values }) => ({ ...values, codes: splitCodes(codesText as string) }),
          canEdit: () => false
        }
      }}
    />
  );
}
