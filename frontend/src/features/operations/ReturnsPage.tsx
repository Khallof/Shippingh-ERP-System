import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { IconButton, Tooltip } from '@mui/material';
import MoveToInboxOutlinedIcon from '@mui/icons-material/MoveToInboxOutlined';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import BlockIcon from '@mui/icons-material/Block';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { ShipmentReturnDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';

type Row = ShipmentReturnDto & GridValidRowModel;

/** Returns to sender: open (by tracking number) -> received back at a branch -> completed (or cancelled). */
export default function ReturnsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [action, setAction] = useState<{ row: Row; kind: 'receive' | 'complete' | 'cancel' } | null>(null);
  const canManage = hasPermission(P.Returns.Manage);

  const submit = async (text: string) => {
    const { row, kind } = action!;
    try {
      const body = kind === 'cancel' ? { reason: text } : kind === 'receive' ? { branchId: null, notes: text || null } : { notes: text || null };
      await api.post(`/shipment-returns/${row.id}/${kind}`, body);
      notify.success(t('common.done'));
      setAction(null);
      await queryClient.invalidateQueries({ queryKey: ['/shipment-returns'] });
    } catch (error) {
      notify.error(error);
    }
  };

  return (
    <>
      <ResourcePage<Row>
        config={{
          title: t('nav.returns'),
          url: '/shipment-returns',
          managePermission: P.Returns.Manage,
          defaultSort: 'createdAtUtc',
          createLabel: tx('مرتجع جديد', 'New return'),
          columns: [
            col.text('returnNumber', tx('الرقم', 'Number'), { minWidth: 130 }),
            col.text('trackingNumber', tx('رقم التتبع', 'Tracking no.'), { minWidth: 140 }),
            col.enumText('returnType', tx('النوع', 'Type'), 'ShipmentReturnType'),
            col.status('status', t('common.status'), 'ShipmentReturnStatus'),
            col.status('shipmentStatus', tx('حالة الشحنة', 'Shipment'), 'ShipmentStatus'),
            col.text('reason', t('common.reason'), { minWidth: 160 }),
            col.text('returnBranchName', tx('فرع الإرجاع', 'Return branch')),
            col.money('returnCharge', tx('أجر الإرجاع', 'Return charge')),
            col.dateTime('createdAtUtc', tx('أُنشئ', 'Created'), S)
          ],
          filters: [
            { name: 'status', label: t('common.status'), type: 'enum', enumName: 'ShipmentReturnStatus' },
            { name: 'returnType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'ShipmentReturnType' },
            { name: 'returnBranchId', label: tx('فرع الإرجاع', 'Return branch'), type: 'lookup', lookup: 'branches' }
          ],
          rowLink: (row) => `/shipments/${row.shipmentId}`,
          rowActions: (row) => canManage && row.status === 'Open' ? (
            <>
              {!row.receivedBackAtUtc && <Tooltip title={tx('استلام المرتجع', 'Receive back')}><IconButton size="small" onClick={() => setAction({ row, kind: 'receive' })}><MoveToInboxOutlinedIcon fontSize="small" /></IconButton></Tooltip>}
              <Tooltip title={tx('إكمال (تسليم للمرسل)', 'Complete')}><IconButton size="small" color="success" onClick={() => setAction({ row, kind: 'complete' })}><TaskAltIcon fontSize="small" /></IconButton></Tooltip>
              <Tooltip title={t('common.cancel')}><IconButton size="small" color="error" onClick={() => setAction({ row, kind: 'cancel' })}><BlockIcon fontSize="small" /></IconButton></Tooltip>
            </>
          ) : null,
          form: {
            fields: [
              { name: 'trackingNumber', label: tx('رقم التتبع', 'Tracking number'), required: true },
              { name: 'returnType', label: tx('نوع الإرجاع', 'Return type'), type: 'enum', enumName: 'ShipmentReturnType', required: true },
              { name: 'returnBranchId', label: tx('فرع الإرجاع', 'Return branch'), type: 'lookup', lookup: 'branches' },
              { name: 'returnCharge', label: tx('أجر الإرجاع', 'Return charge'), type: 'money', min: 0 },
              { name: 'reason', label: t('common.reason'), required: true, span: 12 },
              { name: 'notes', label: t('common.notes'), type: 'textarea' }
            ],
            toValues: () => ({ trackingNumber: '', returnType: 'FailedDelivery', returnBranchId: null, returnCharge: 0, reason: '', notes: null }),
            // The API wants the shipment id: resolve the scanned tracking number first.
            toRequest: async ({ trackingNumber, ...values }) => ({ ...values, shipmentId: (await shipmentsApi.track(String(trackingNumber).trim())).shipmentId }),
            canEdit: () => false
          }
        }}
      />
      <ReasonDialog open={!!action} required={action?.kind === 'cancel'} danger={action?.kind === 'cancel'}
        title={action?.kind === 'cancel' ? t('common.cancel') : action?.kind === 'receive' ? tx('استلام المرتجع في الفرع', 'Receive at branch') : tx('إكمال المرتجع', 'Complete return')}
        label={action?.kind === 'cancel' ? t('common.reason') : t('common.notes')} onClose={() => setAction(null)} onSubmit={(text) => void submit(text)} />
    </>
  );
}
