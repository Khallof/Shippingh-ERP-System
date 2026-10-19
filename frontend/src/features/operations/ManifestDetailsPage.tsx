import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, CircularProgress, IconButton, Stack, TableCell } from '@mui/material';
import LocalShippingOutlinedIcon from '@mui/icons-material/LocalShippingOutlined';
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { DriverManifestDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import StatusChip from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { PrintButton } from '@/components/common/ExportMenu';
import { ConfirmDialog, ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import ScanBox from '@/components/form/ScanBox';
import { useTx } from '@/i18n';
import { formatDate, formatDateTime, formatMoney } from '@/utils/format';
import ManifestItemsTable from './ManifestItemsTable';

/** Manifest: scan shipments, print the PDF run sheet (signatures), dispatch, close with a reason for open items. */
export default function ManifestDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [dialog, setDialog] = useState<'close' | 'cancel' | null>(null);
  const [busy, setBusy] = useState(false);
  const manifest = useQuery({ queryKey: ['/driver-manifests', id], queryFn: () => api.get<DriverManifestDto>(`/driver-manifests/${id}`) });

  const run = async (action: () => Promise<unknown>, message = t('common.done')) => {
    setBusy(true);
    try {
      await action();
      notify.success(message);
      await queryClient.invalidateQueries({ queryKey: ['/driver-manifests'] });
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  if (manifest.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (!manifest.data) return <ErrorAlert error={manifest.error} />;
  const m = manifest.data;
  const canManage = hasPermission(P.DeliveryManifests.Manage);
  const draft = m.status === 'Draft';

  return (
    <>
      <PageHeader back="/manifests" title={<Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}><span>{m.manifestNumber}</span><StatusChip enumName="DriverManifestStatus" value={m.status} /></Stack>}
        subtitle={`${m.driverName} · ${formatDate(m.manifestDateUtc)} · ${m.branchName}`}
        actions={
          <>
            <PrintButton url={`/driver-manifests/${id}/pdf`} label={tx('طباعة الكشف', 'Print manifest')} icon={<PictureAsPdfOutlinedIcon />} />
            {draft && canManage && <Button variant="contained" startIcon={<LocalShippingOutlinedIcon />} disabled={busy || m.itemCount === 0} onClick={() => void run(() => api.post(`/driver-manifests/${id}/dispatch`), tx('خرج للتوزيع', 'Dispatched'))}>{tx('إرسال للتوزيع', 'Dispatch')}</Button>}
            {m.status === 'Dispatched' && canManage && <Button variant="contained" color="success" startIcon={<TaskAltIcon />} disabled={busy} onClick={() => setDialog('close')}>{tx('إغلاق الكشف', 'Close manifest')}</Button>}
            {(draft || m.status === 'Dispatched') && canManage && <Button color="error" onClick={() => setDialog('cancel')}>{t('common.cancel')}</Button>}
          </>
        } />
      <Section>
        <InfoGrid items={[
          { label: tx('المركبة', 'Vehicle'), value: m.vehiclePlateNumber }, { label: tx('الشحنات', 'Shipments'), value: m.itemCount },
          { label: tx('سُلّمت / فشلت / متبقية', 'Delivered / failed / pending'), value: `${m.deliveredCount} / ${m.failedCount} / ${m.pendingCount}` },
          { label: tx('إجمالي التحصيل', 'COD total'), value: formatMoney(m.totalCodAmount) },
          { label: tx('خرج', 'Dispatched'), value: formatDateTime(m.dispatchedAtUtc) }, { label: tx('أُغلق', 'Closed'), value: formatDateTime(m.closedAtUtc) },
          { label: t('common.notes'), value: m.notes, span: 6 }
        ]} />
      </Section>
      {draft && canManage && (
        <Section title={tx('إضافة شحنات', 'Add shipments')}>
          <ScanBox busy={busy} onCodes={(codes) => run(() => api.post(`/driver-manifests/${id}/items`, { codes }), tx('أُضيفت', 'Added'))} />
        </Section>
      )}
      <Section title={tx('الشحنات', 'Shipments')} noPadding>
        <ManifestItemsTable
          items={m.items ?? []}
          extraHead={<><TableCell>{tx('النتيجة', 'Result')}</TableCell><TableCell /></>}
          extra={(item) => (
            <>
              <TableCell><StatusChip enumName="DeliveryResult" value={item.result} />{item.failureReason && <Box component="span" sx={{ display: 'block', fontSize: 12, opacity: 0.7 }}>{item.failureReason}</Box>}</TableCell>
              <TableCell>{draft && canManage && <IconButton size="small" color="error" onClick={() => void run(() => api.delete(`/driver-manifests/${id}/items/${item.shipmentId}`))}><DeleteOutlineIcon fontSize="small" /></IconButton>}</TableCell>
            </>
          )}
        />
      </Section>
      <ReasonDialog open={dialog === 'close'} required={m.pendingCount > 0} title={tx('إغلاق الكشف', 'Close manifest')} label={m.pendingCount > 0 ? tx(`سبب بقاء ${m.pendingCount} شحنة دون نتيجة`, `Reason for ${m.pendingCount} pending shipments`) : t('common.notes')}
        onClose={() => setDialog(null)} onSubmit={(text) => { setDialog(null); void run(() => api.post(`/driver-manifests/${id}/close`, m.pendingCount > 0 ? { pendingReason: text, notes: null } : { pendingReason: null, notes: text || null })); }} />
      <ConfirmDialog open={dialog === 'cancel'} danger title={tx('إلغاء الكشف', 'Cancel manifest')} onClose={() => setDialog(null)} onConfirm={() => { setDialog(null); void run(() => api.post(`/driver-manifests/${id}/cancel`)); }} />
    </>
  );
}
