import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { IconButton, Tooltip } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { PriceOverrideDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';
import { formatPercent } from '@/utils/format';

type Row = PriceOverrideDto & GridValidRowModel;

/** Manual prices / discounts above the threshold wait here for approval (Pricing.ApproveOverride). */
export default function PriceOverridesPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [decision, setDecision] = useState<{ row: Row; approve: boolean } | null>(null);

  return (
    <>
      <ResourcePage<Row>
        config={{
          title: t('nav.priceOverrides'),
          url: '/price-overrides',
          defaultSort: 'createdAtUtc',
          rowLink: (row) => `/shipments/${row.shipmentId}`,
          columns: [
            col.text('trackingNumber', tx('رقم التتبع', 'Tracking no.'), { minWidth: 140 }),
            col.text('senderName', tx('المرسل', 'Sender')),
            col.status('status', t('common.status'), 'PriceOverrideStatus', S),
            col.money('calculatedTotalShippingCharge', tx('السعر المحسوب', 'Calculated'), 'currencyCode'),
            col.money('proposedTotalShippingCharge', tx('السعر المقترح', 'Proposed'), 'currencyCode'),
            col.number('deviationPercentage', tx('الانحراف', 'Deviation'), 2, { ...S, valueFormatter: (v: number) => formatPercent(v) }),
            col.text('reason', t('common.reason'), { minWidth: 160 }),
            col.text('requestedByUserName', tx('طلبه', 'Requested by')),
            col.dateTime('requestedAtUtc', tx('وقت الطلب', 'Requested at')),
            col.text('decidedByUserName', tx('القرار من', 'Decided by'))
          ],
          filters: [{ name: 'status', label: t('common.status'), type: 'enum', enumName: 'PriceOverrideStatus' }],
          rowActions: (row) =>
            row.status === 'Pending' && hasPermission(P.Pricing.ApproveOverride) ? (
              <>
                <Tooltip title={t('common.approve')}><IconButton size="small" color="success" onClick={() => setDecision({ row, approve: true })}><CheckIcon fontSize="small" /></IconButton></Tooltip>
                <Tooltip title={t('common.reject')}><IconButton size="small" color="error" onClick={() => setDecision({ row, approve: false })}><CloseIcon fontSize="small" /></IconButton></Tooltip>
              </>
            ) : null
        }}
      />
      <ReasonDialog
        open={!!decision}
        required={!decision?.approve}
        title={decision?.approve ? t('common.approve') : t('common.reject')}
        label={t('common.notes')}
        danger={!decision?.approve}
        onClose={() => setDecision(null)}
        onSubmit={async (notes) => {
          try {
            await api.post(`/price-overrides/${decision!.row.id}/${decision!.approve ? 'approve' : 'reject'}`, { notes: notes || null });
            notify.success(t('common.done'));
            setDecision(null);
            await queryClient.invalidateQueries({ queryKey: ['/price-overrides'] });
          } catch (error) {
            notify.error(error);
          }
        }}
      />
    </>
  );
}
