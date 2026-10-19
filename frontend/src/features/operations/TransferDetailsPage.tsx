import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Chip, CircularProgress, IconButton, Stack, TableCell } from '@mui/material';
import LocalShippingOutlinedIcon from '@mui/icons-material/LocalShippingOutlined';
import MoveToInboxOutlinedIcon from '@mui/icons-material/MoveToInboxOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import CheckIcon from '@mui/icons-material/Check';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { BranchTransferDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import StatusChip from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { ConfirmDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import ScanBox from '@/components/form/ScanBox';
import { useTx } from '@/i18n';
import { formatDateTime } from '@/utils/format';
import ManifestItemsTable from './ManifestItemsTable';

/**
 * Transfer: scan shipments into the draft, dispatch (shipments InTransit), then the receiving branch scans what
 * arrived; unscanned shipments become Missing on receive.
 */
export default function TransferDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [received, setReceived] = useState<string[]>([]);
  const [cancel, setCancel] = useState(false);
  const [busy, setBusy] = useState(false);

  const transfer = useQuery({ queryKey: ['/branch-transfers', id], queryFn: () => api.get<BranchTransferDto>(`/branch-transfers/${id}`) });

  const run = async (action: () => Promise<unknown>, message = t('common.done')) => {
    setBusy(true);
    try {
      await action();
      notify.success(message);
      await queryClient.invalidateQueries({ queryKey: ['/branch-transfers'] });
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  if (transfer.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (!transfer.data) return <ErrorAlert error={transfer.error} />;
  const tr = transfer.data;
  const draft = tr.status === 'Draft';
  const dispatched = tr.status === 'Dispatched';
  const canManage = hasPermission(P.Transfers.Manage);
  const canReceive = hasPermission(P.Transfers.Receive);
  const codes = new Set(received.map((c) => c.toUpperCase()));
  const isScanned = (tracking: string) => codes.has(tracking.toUpperCase());

  return (
    <>
      <PageHeader back="/transfers" title={<Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}><span>{tr.transferNumber}</span><StatusChip enumName="BranchTransferStatus" value={tr.status} /></Stack>}
        subtitle={`${tr.fromBranchName} ← ${tr.toBranchName}`}
        actions={
          <>
            {draft && canManage && <Button variant="contained" startIcon={<LocalShippingOutlinedIcon />} disabled={busy || tr.itemCount === 0} onClick={() => void run(() => api.post(`/branch-transfers/${id}/dispatch`, {}), tx('تم الإرسال', 'Dispatched'))}>{tx('إرسال', 'Dispatch')}</Button>}
            {dispatched && canReceive && <Button variant="contained" color="success" startIcon={<MoveToInboxOutlinedIcon />} disabled={busy} onClick={() => void run(() => api.post(`/branch-transfers/${id}/receive`, { receivedCodes: received, notes: null }), tx('تم الاستلام', 'Received'))}>{tx('تأكيد الاستلام', 'Confirm receipt')} ({received.length}/{tr.itemCount})</Button>}
            {(draft || dispatched) && canManage && <Button color="error" disabled={busy} onClick={() => setCancel(true)}>{t('common.cancel')}</Button>}
          </>
        } />
      <Section>
        <InfoGrid items={[
          { label: t('common.driver'), value: tr.driverName }, { label: tx('المركبة', 'Vehicle'), value: tr.vehiclePlateNumber }, { label: tx('الختم', 'Seal'), value: tr.sealNumber },
          { label: tx('أُرسل', 'Dispatched'), value: formatDateTime(tr.dispatchedAtUtc) }, { label: tx('استُلم', 'Received'), value: formatDateTime(tr.receivedAtUtc) },
          { label: tx('المستلمة / المفقودة', 'Received / missing'), value: `${tr.receivedCount} / ${tr.missingCount}` }, { label: t('common.notes'), value: tr.notes, span: 6 }
        ]} />
      </Section>
      {draft && canManage && (
        <Section title={tx('إضافة شحنات', 'Add shipments')}>
          <ScanBox busy={busy} onCodes={(list) => run(() => api.post(`/branch-transfers/${id}/items`, { codes: list }), tx('أُضيفت', 'Added'))} />
        </Section>
      )}
      {dispatched && canReceive && (
        <Section title={tx('مسح الشحنات الواصلة', 'Scan arrived shipments')}>
          <Alert severity="info" sx={{ mb: 2 }}>{tx('الشحنات غير الممسوحة ستُسجّل مفقودة عند تأكيد الاستلام.', 'Shipments not scanned are recorded as missing on receipt.')}</Alert>
          <ScanBox onCodes={(list) => setReceived((current) => Array.from(new Set([...current, ...list])))} buttonText={tx('مسح', 'Scan')} />
        </Section>
      )}
      <Section title={`${tx('الشحنات', 'Shipments')} (${tr.itemCount})`} noPadding>
        <ManifestItemsTable
          items={tr.items ?? []}
          extraHead={<TableCell>{tx('حالة البند', 'Item')}</TableCell>}
          extra={(item) => (
            <TableCell>
              {dispatched && canReceive ? (isScanned(item.trackingNumber) ? <Chip color="success" icon={<CheckIcon />} label={tx('ممسوحة', 'Scanned')} /> : <Chip label={tx('لم تُمسح', 'Not scanned')} />) : <StatusChip enumName="BranchTransferItemStatus" value={item.itemStatus} />}
              {draft && canManage && <IconButton size="small" color="error" onClick={() => void run(() => api.delete(`/branch-transfers/${id}/items/${item.shipmentId}`))}><DeleteOutlineIcon fontSize="small" /></IconButton>}
            </TableCell>
          )}
        />
      </Section>
      <ConfirmDialog open={cancel} danger title={tx('إلغاء التحويل', 'Cancel transfer')} onClose={() => setCancel(false)} onConfirm={() => { setCancel(false); void run(() => api.post(`/branch-transfers/${id}/cancel`)); }} />
    </>
  );
}
