import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Card, CardActions, CardContent, Chip, CircularProgress, Grid2 as Grid, Stack, Tab, Tabs, Typography } from '@mui/material';
import PhoneOutlinedIcon from '@mui/icons-material/PhoneOutlined';
import PlaceOutlinedIcon from '@mui/icons-material/PlaceOutlined';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import ReportProblemOutlinedIcon from '@mui/icons-material/ReportProblemOutlined';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { DriverManifestDto, DriverManifestItemDto } from '@/api/generated';
import type { PagedResult } from '@/api/types';
import PageHeader from '@/components/common/PageHeader';
import StatusChip from '@/components/common/StatusChip';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import DeliverDialog from '@/features/shipments/DeliverDialog';
import { useTx } from '@/i18n';
import { formatDate, formatMoney } from '@/utils/format';

/**
 * Driver screen (phone first): the dispatched manifests of the signed-in driver (the API returns only his own when
 * he has the Driver role), one card per stop with call / map buttons, deliver with proof (signature + photo) or fail.
 */
export default function DriverPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [manifestIndex, setManifestIndex] = useState(0);
  const [delivering, setDelivering] = useState<DriverManifestItemDto | null>(null);
  const [failing, setFailing] = useState<DriverManifestItemDto | null>(null);

  const manifests = useQuery({
    queryKey: ['/driver-manifests', 'driver-screen'],
    queryFn: () => api.get<PagedResult<DriverManifestDto>>('/driver-manifests', { status: 'Dispatched', pageSize: 20 }),
    refetchInterval: 60_000
  });
  const current = manifests.data?.items[manifestIndex];
  const details = useQuery({
    queryKey: ['/driver-manifests', current?.id],
    queryFn: () => api.get<DriverManifestDto>(`/driver-manifests/${current!.id}`),
    enabled: !!current
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['/driver-manifests'] });
  const items = details.data?.items ?? [];
  const pending = items.filter((item) => item.result === 'Pending');
  const done = items.filter((item) => item.result !== 'Pending');
  const collected = items.filter((item) => item.result === 'Delivered' && item.isCOD).reduce((sum, item) => sum + item.codAmount, 0);

  return (
    <>
      <PageHeader title={t('nav.driver')} subtitle={current ? `${current.manifestNumber} · ${formatDate(current.manifestDateUtc)}` : undefined} />
      {manifests.isLoading && <CircularProgress />}
      {manifests.data?.items.length === 0 && <Alert severity="info">{tx('لا توجد كشوف توزيع خارجة باسمك حالياً.', 'You have no dispatched manifests right now.')}</Alert>}
      {(manifests.data?.items.length ?? 0) > 1 && (
        <Tabs value={manifestIndex} onChange={(_, v: number) => setManifestIndex(v)} variant="scrollable" sx={{ mb: 2 }}>
          {manifests.data!.items.map((m) => <Tab key={m.id} label={m.manifestNumber} />)}
        </Tabs>
      )}
      {details.data && (
        <>
          <Grid container spacing={1} sx={{ mb: 2 }}>
            <Grid size={4}><Card><CardContent sx={{ p: 1.5 }}><Typography variant="caption">{tx('متبقية', 'Pending')}</Typography><Typography variant="h5">{pending.length}</Typography></CardContent></Card></Grid>
            <Grid size={4}><Card><CardContent sx={{ p: 1.5 }}><Typography variant="caption">{tx('منجزة', 'Done')}</Typography><Typography variant="h5">{done.length}</Typography></CardContent></Card></Grid>
            <Grid size={4}><Card><CardContent sx={{ p: 1.5 }}><Typography variant="caption">{tx('التحصيل', 'Collected')}</Typography><Typography variant="h6" sx={{ fontVariantNumeric: 'tabular-nums' }}>{formatMoney(collected)}</Typography></CardContent></Card></Grid>
          </Grid>
          <Stack spacing={1.5}>
            {[...pending, ...done].map((item) => (
              <Card key={item.itemId} sx={{ opacity: item.result === 'Pending' ? 1 : 0.65 }}>
                <CardContent sx={{ pb: 1 }}>
                  <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', gap: 1 }}>
                    <Typography variant="subtitle1" fontWeight={700}>{item.sequence}. {item.recipientName}</Typography>
                    {item.result === 'Pending' ? <StatusChip enumName="ShipmentStatus" value={item.shipmentStatus} /> : <StatusChip enumName="DeliveryResult" value={item.result} />}
                  </Stack>
                  <Typography variant="body2" color="text.secondary" dir="ltr" sx={{ textAlign: 'start' }}>{item.trackingNumber}</Typography>
                  <Typography variant="body2" sx={{ mt: 0.5 }}>{[item.destinationCityName, item.destinationZoneName, item.recipientAddressLine].filter(Boolean).join(' - ')}</Typography>
                  <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                    <Chip label={`${item.packageCount} ${tx('طرد', 'pcs')}`} />
                    {item.isCOD && <Chip color="warning" label={`${tx('حصّل', 'Collect')} ${formatMoney(item.codAmount)}`} />}
                    {item.chargesOnRecipient && item.chargesToCollect > 0 && <Chip color="warning" variant="outlined" label={`${tx('أجور الشحن', 'Charges')} ${formatMoney(item.chargesToCollect)}`} />}
                  </Stack>
                </CardContent>
                <CardActions sx={{ flexWrap: 'wrap', gap: 1 }}>
                  <Button size="small" startIcon={<PhoneOutlinedIcon />} href={`tel:${item.recipientPhone}`}>{tx('اتصال', 'Call')}</Button>
                  <Button size="small" startIcon={<PlaceOutlinedIcon />} href={`https://maps.google.com/?q=${encodeURIComponent([item.recipientAddressLine, item.destinationCityName].filter(Boolean).join(' '))}`} target="_blank" rel="noreferrer">{tx('الخريطة', 'Map')}</Button>
                  <Box sx={{ flex: 1 }} />
                  {item.result === 'Pending' && item.shipmentStatus === 'OutForDelivery' && (
                    <>
                      <Button size="small" color="error" startIcon={<ReportProblemOutlinedIcon />} onClick={() => setFailing(item)}>{tx('فشل', 'Failed')}</Button>
                      <Button size="small" variant="contained" color="success" startIcon={<TaskAltIcon />} onClick={() => setDelivering(item)}>{tx('تسليم', 'Deliver')}</Button>
                    </>
                  )}
                </CardActions>
              </Card>
            ))}
          </Stack>
        </>
      )}
      {delivering && (
        <DeliverDialog open shipmentId={delivering.shipmentId} trackingNumber={delivering.trackingNumber} defaultRecipient={delivering.recipientName} codAmount={delivering.isCOD ? delivering.codAmount : 0} chargesAmount={delivering.chargesToCollect}
          onClose={() => setDelivering(null)} onDelivered={() => { setDelivering(null); notify.success(tx('تم تأكيد التسليم', 'Delivery confirmed')); void refresh(); }} />
      )}
      <ReasonDialog open={!!failing} title={tx('سبب فشل التسليم', 'Failure reason')} onClose={() => setFailing(null)}
        onSubmit={async (reason) => {
          try {
            await shipmentsApi.deliveryFailed(failing!.shipmentId, { reason });
            setFailing(null);
            notify.success(t('common.done'));
            await refresh();
          } catch (error) {
            notify.error(error);
          }
        }} />
    </>
  );
}
