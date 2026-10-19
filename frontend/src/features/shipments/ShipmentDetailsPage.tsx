import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Card, Chip, CircularProgress, Grid2 as Grid, IconButton, ListItemIcon, Menu, MenuItem, Stack, Tab, Table,
  TableBody, TableCell, TableHead, TableRow, Tabs, TextField, Typography
} from '@mui/material';
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import MoreVertIcon from '@mui/icons-material/MoreVert';
import SyncAltIcon from '@mui/icons-material/SyncAlt';
import TwoWheelerOutlinedIcon from '@mui/icons-material/TwoWheelerOutlined';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import ReportProblemOutlinedIcon from '@mui/icons-material/ReportProblemOutlined';
import BlockIcon from '@mui/icons-material/Block';
import BuildOutlinedIcon from '@mui/icons-material/BuildOutlined';
import ReceiptLongOutlinedIcon from '@mui/icons-material/ReceiptLongOutlined';
import UploadFileOutlinedIcon from '@mui/icons-material/UploadFileOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined';
import { useTranslation } from 'react-i18next';
import { api, apiPath, openFile } from '@/api/client';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { InvoiceDto, ShipmentAttachmentType } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import StatusChip from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import { useLanguage, useTx } from '@/i18n';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { formatDateTime, formatMoney, formatNumber } from '@/utils/format';
import ShipmentTimeline from './ShipmentTimeline';
import DeliverDialog from './DeliverDialog';
import { AssignDriverDialog, ChangeStatusDialog, CorrectStatusDialog, InvoiceFromShipmentDialog } from './ShipmentActionDialogs';

type DialogName = 'status' | 'correct' | 'driver' | 'deliver' | 'failed' | 'cancel' | 'invoice' | null;

/** Shipment details: summary, actions by permission and status, tracking timeline, packages, pricing, POD, files. */
/** Up to 3 decimals without trailing zeros (240 / 1.75 / 2.125). */
function formatQty(value: number): string {
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 }).format(value);
}

export default function ShipmentDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [tab, setTab] = useState(0);
  const [dialog, setDialog] = useState<DialogName>(null);
  const [menu, setMenu] = useState<HTMLElement | null>(null);
  const [busy, setBusy] = useState(false);
  const [attachmentType, setAttachmentType] = useState<ShipmentAttachmentType>('Document');

  const shipment = useQuery({ queryKey: ['/shipments', id], queryFn: () => shipmentsApi.get(id) });
  const history = useQuery({ queryKey: ['/shipments', id, 'history'], queryFn: () => shipmentsApi.history(id) });
  const pricing = useQuery({ queryKey: ['/shipments', id, 'pricing'], queryFn: () => shipmentsApi.pricing(id), enabled: tab === 2 });
  const proof = useQuery({
    queryKey: ['/shipments', id, 'pod'],
    queryFn: () => shipmentsApi.proofOfDelivery(id),
    enabled: tab === 3 && shipment.data?.status === 'Delivered',
    retry: false
  });
  const attachments = useQuery({ queryKey: ['/shipments', id, 'attachments'], queryFn: () => shipmentsApi.attachments(id), enabled: tab === 4 });

  const refresh = async () => {
    setDialog(null);
    await queryClient.invalidateQueries({ queryKey: ['/shipments'] });
  };

  const act = async (action: () => Promise<unknown>, message = t('common.done')) => {
    await action();
    notify.success(message);
    await refresh();
  };

  const print = async (format: 'Pdf' | 'Html') => {
    setBusy(true);
    try {
      openFile(await shipmentsApi.label(id, format));
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  const download = async (url: string, name: string) => {
    try {
      openFile(await api.download(apiPath(url), { fallbackName: name }));
    } catch (error) {
      notify.error(error);
    }
  };

  if (shipment.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (shipment.error || !shipment.data) return <ErrorAlert error={shipment.error} onRetry={() => void shipment.refetch()} />;

  const s = shipment.data;
  const money = (value: number | null | undefined) => formatMoney(value, s.currencyCode, language);
  const finished = ['Delivered', 'Returned', 'Cancelled', 'LostOrDamaged'].includes(s.status);
  const canDeliver = s.status === 'OutForDelivery' && hasPermission(P.Shipments.ConfirmDelivery);

  return (
    <>
      <PageHeader
        back="/shipments"
        title={<Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}><span dir="ltr">{s.trackingNumber}</span><StatusChip enumName="ShipmentStatus" value={s.status} /></Stack>}
        subtitle={`${s.shipmentNumber} · ${s.serviceTypeName} · ${formatDateTime(s.createdAtUtc)}`}
        actions={
          <>
            <Button variant="outlined" startIcon={<PrintOutlinedIcon />} onClick={() => void print('Pdf')} disabled={busy}>{tx('طباعة الملصق', 'Print label')}</Button>
            {canDeliver && <Button variant="contained" color="success" startIcon={<TaskAltIcon />} onClick={() => setDialog('deliver')}>{tx('تأكيد التسليم', 'Deliver')}</Button>}
            {!finished && hasPermission(P.Shipments.Update) && ['Created', 'PickedUp', 'ReceivedAtBranch'].includes(s.status) && (
              <Button variant="outlined" startIcon={<EditOutlinedIcon />} onClick={() => navigate(`/shipments/${id}/edit`)}>{t('common.edit')}</Button>
            )}
            <IconButton onClick={(event) => setMenu(event.currentTarget)} aria-label={t('common.actions')}><MoreVertIcon /></IconButton>
            <Menu anchorEl={menu} open={!!menu} onClose={() => setMenu(null)} onClick={() => setMenu(null)}>
              <MenuItem onClick={() => void print('Html')}><ListItemIcon><PrintOutlinedIcon fontSize="small" /></ListItemIcon>{tx('ملصق HTML (طابعة حرارية)', 'HTML label (thermal printer)')}</MenuItem>
              {hasPermission(P.Shipments.ChangeStatus) && !finished && <MenuItem onClick={() => setDialog('status')}><ListItemIcon><SyncAltIcon fontSize="small" /></ListItemIcon>{tx('تغيير الحالة', 'Change status')}</MenuItem>}
              {hasPermission(P.Shipments.AssignDriver) && !finished && <MenuItem onClick={() => setDialog('driver')}><ListItemIcon><TwoWheelerOutlinedIcon fontSize="small" /></ListItemIcon>{tx('إسناد سائق', 'Assign driver')}</MenuItem>}
              {s.status === 'OutForDelivery' && hasPermission(P.Shipments.ConfirmDelivery) && <MenuItem onClick={() => setDialog('failed')}><ListItemIcon><ReportProblemOutlinedIcon fontSize="small" /></ListItemIcon>{tx('تسجيل فشل التسليم', 'Record failed delivery')}</MenuItem>}
              {hasPermission(P.Invoices.Create) && !s.invoiceId && s.status !== 'Cancelled' && <MenuItem onClick={() => setDialog('invoice')}><ListItemIcon><ReceiptLongOutlinedIcon fontSize="small" /></ListItemIcon>{tx('إنشاء فاتورة', 'Create invoice')}</MenuItem>}
              {hasPermission(P.Shipments.CorrectStatus) && <MenuItem onClick={() => setDialog('correct')}><ListItemIcon><BuildOutlinedIcon fontSize="small" /></ListItemIcon>{tx('تصحيح الحالة', 'Correct status')}</MenuItem>}
              {hasPermission(P.Shipments.Cancel) && s.nextStatuses.includes('Cancelled') && <MenuItem onClick={() => setDialog('cancel')} sx={{ color: 'error.main' }}><ListItemIcon><BlockIcon fontSize="small" color="error" /></ListItemIcon>{tx('إلغاء الشحنة', 'Cancel shipment')}</MenuItem>}
            </Menu>
          </>
        }
      />

      {s.hasPendingPriceOverride && <Alert severity="warning" sx={{ mb: 2 }}>{tx('يوجد سعر يدوي بانتظار الاعتماد.', 'A manual price is waiting for approval.')}</Alert>}
      {s.cancellationReason && <Alert severity="info" sx={{ mb: 2 }}>{tx('سبب الإلغاء', 'Cancellation reason')}: {s.cancellationReason}</Alert>}

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Section title={tx('المرسل والمستلم', 'Sender and recipient')}>
            <InfoGrid items={[
              { label: tx('المرسل', 'Sender'), value: `${s.senderCode} - ${s.senderName}`, span: 4 },
              { label: tx('هاتف المرسل', 'Sender phone'), value: <span dir="ltr">{s.senderPhone}</span>, span: 3 },
              { label: tx('عنوان المرسل', 'Sender address'), value: s.senderAddressLine, span: 5 },
              { label: tx('المستلم', 'Recipient'), value: `${s.recipientCode} - ${s.recipientName}`, span: 4 },
              { label: tx('هاتف المستلم', 'Recipient phone'), value: <span dir="ltr">{s.recipientContactPhone ?? s.recipientPhone}</span>, span: 3 },
              { label: tx('عنوان المستلم', 'Recipient address'), value: s.recipientAddressLine, span: 5 }
            ]} />
          </Section>
          <Section title={tx('المسار', 'Route')}>
            <InfoGrid items={[
              { label: tx('من', 'From'), value: `${s.originCityName} · ${s.originBranchName}` },
              { label: tx('إلى', 'To'), value: [s.destinationCityName, s.destinationZoneName, s.destinationBranchName].filter(Boolean).join(' · ') },
              { label: tx('الموقع الحالي', 'Current location'), value: [s.currentBranchName, s.currentWarehouseName].filter(Boolean).join(' · ') },
              { label: t('common.driver'), value: [s.assignedDriverName, s.vehiclePlateNumber].filter(Boolean).join(' · ') },
              { label: tx('محاولات التسليم', 'Delivery attempts'), value: formatNumber(s.deliveryAttemptCount) },
              { label: tx('تاريخ التسليم', 'Delivered at'), value: formatDateTime(s.deliveredAtUtc) },
              { label: tx('المحتوى', 'Contents'), value: `${s.packageDescription} · ${s.serviceTypeName}` },
              { label: tx('الطرود والوزن والحجم', 'Packages, weight, volume'), value: [
                `${formatNumber(s.packageCount)} ${tx('طرد', 'pcs')}`,
                `${formatQty(s.actualWeight)} ${tx('كغ', 'kg')}`,
                ...(s.totalVolumeCbm > 0 ? [`${formatQty(s.totalVolumeCbm)} ${tx('م³', 'm³')}`] : [])
              ].join(' · ') },
              { label: tx('طريقة التسعير', 'Pricing basis'), value: enumLabel('PricingBasis', s.pricingBasis, language) },
              { label: tx('أجور الشحن', 'Shipping charges'), value: enumLabel('ChargesPayer', s.chargesPaidBy, language) },
              ...(s.externalReference ? [{ label: tx('المرجع الخارجي', 'External reference'), value: s.externalReference }] : [])
            ]} />
          </Section>

          <Card>
            <Tabs value={tab} onChange={(_, value: number) => setTab(value)} variant="scrollable" sx={{ borderBottom: 1, borderColor: 'divider', px: 1 }}>
              <Tab label={tx('سجل التتبع', 'Tracking')} />
              <Tab label={`${tx('الطرود', 'Packages')} (${s.packageCount})`} />
              <Tab label={tx('التسعير', 'Pricing')} />
              <Tab label={tx('إثبات التسليم', 'Proof of delivery')} />
              <Tab label={tx('المرفقات', 'Attachments')} />
            </Tabs>
            <Box sx={{ p: 2 }}>
              {tab === 0 && (history.isLoading ? <CircularProgress size={24} /> : history.data ? <ShipmentTimeline history={history.data} /> : <ErrorAlert error={history.error} />)}
              {tab === 1 && (
                <Table size="small">
                  <TableHead><TableRow><TableCell>#</TableCell><TableCell>{tx('باركود الطرد', 'Barcode')}</TableCell><TableCell>{tx('الوصف', 'Description')}</TableCell><TableCell>{tx('الوزن', 'Weight')}</TableCell><TableCell>{tx('الأبعاد سم', 'Dimensions cm')}</TableCell><TableCell>{tx('الحجمي', 'Volumetric')}</TableCell></TableRow></TableHead>
                  <TableBody>
                    {s.packages.map((p) => (
                      <TableRow key={p.id}>
                        <TableCell>{p.sequenceNumber}</TableCell>
                        <TableCell dir="ltr">{p.packageBarcode}</TableCell>
                        <TableCell>{p.description ?? '-'}</TableCell>
                        <TableCell>{formatNumber(p.weightKg, 2)}</TableCell>
                        <TableCell dir="ltr">{p.lengthCm ? `${p.lengthCm} × ${p.widthCm} × ${p.heightCm}` : '-'}</TableCell>
                        <TableCell>{formatNumber(p.volumetricWeightKg, 2)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
              {tab === 2 && (
                <Table size="small">
                  <TableHead><TableRow><TableCell>{t('common.date')}</TableCell><TableCell>{t('common.reason')}</TableCell><TableCell>{tx('المصدر', 'Source')}</TableCell><TableCell>{tx('الوزن المحتسب', 'Chargeable')}</TableCell><TableCell>{tx('الشحن', 'Shipping')}</TableCell><TableCell>{tx('الضريبة', 'Tax')}</TableCell><TableCell>{t('common.total')}</TableCell></TableRow></TableHead>
                  <TableBody>
                    {pricing.data?.map((row, index) => (
                      <TableRow key={row.id ?? index}>
                        <TableCell>{formatDateTime(row.calculatedAtUtc)}</TableCell>
                        <TableCell>{row.reason}{row.overrideReason ? ` · ${row.overrideReason}` : ''}</TableCell>
                        <TableCell>{row.pricingRuleName ?? row.pricingSource}</TableCell>
                        <TableCell>{formatNumber(row.chargeableWeight, 2)}</TableCell>
                        <TableCell>{money(row.totalShippingCharge)}</TableCell>
                        <TableCell>{money(row.taxAmount)}</TableCell>
                        <TableCell><strong>{money(row.totalCharge)}</strong></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
              {tab === 3 && (s.status !== 'Delivered' ? (
                <Typography color="text.secondary">{tx('لم تُسلّم الشحنة بعد.', 'Not delivered yet.')}</Typography>
              ) : proof.data ? (
                <>
                  <InfoGrid items={[
                    { label: tx('المستلم', 'Received by'), value: `${proof.data.recipientName}${proof.data.recipientRelation ? ` (${proof.data.recipientRelation})` : ''}` },
                    { label: tx('وقت التسليم', 'Delivered at'), value: formatDateTime(proof.data.deliveredAtUtc) },
                    { label: t('common.driver'), value: proof.data.driverName },
                    { label: tx('الموقع', 'Location'), value: proof.data.latitude ? <a href={`https://maps.google.com/?q=${proof.data.latitude},${proof.data.longitude}`} target="_blank" rel="noreferrer">{proof.data.latitude}, {proof.data.longitude}</a> : '-' },
                    { label: t('common.notes'), value: proof.data.notes, span: 12 }
                  ]} />
                  <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                    {proof.data.signatureUrl && <Button startIcon={<DownloadOutlinedIcon />} onClick={() => void download(proof.data!.signatureUrl!, 'signature.png')}>{tx('التوقيع', 'Signature')}</Button>}
                    {proof.data.photoUrl && <Button startIcon={<DownloadOutlinedIcon />} onClick={() => void download(proof.data!.photoUrl!, 'photo.jpg')}>{tx('الصورة', 'Photo')}</Button>}
                  </Stack>
                </>
              ) : proof.isLoading ? <CircularProgress size={24} /> : <ErrorAlert error={proof.error} />)}
              {tab === 4 && (
                <>
                  {hasPermission(P.Shipments.Update) && (
                    <Stack direction="row" spacing={1} sx={{ mb: 2, alignItems: 'center' }}>
                      <Box sx={{ width: 220 }}>
                        <TextField select label={tx('نوع الملف', 'File type')} value={attachmentType} onChange={(e) => setAttachmentType(e.target.value as ShipmentAttachmentType)}>
                          {enumOptions('ShipmentAttachmentType', language).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
                        </TextField>
                      </Box>
                      <Button component="label" variant="outlined" startIcon={<UploadFileOutlinedIcon />}>
                        {tx('رفع ملف', 'Upload file')}
                        <input hidden type="file" accept="image/jpeg,image/png,image/webp,application/pdf" onChange={async (event) => {
                          const file = event.target.files?.[0];
                          event.target.value = '';
                          if (!file) return;
                          try {
                            await act(() => shipmentsApi.uploadAttachment(id, file, attachmentType), t('common.saved'));
                          } catch (error) {
                            notify.error(error);
                          }
                        }} />
                      </Button>
                    </Stack>
                  )}
                  <Table size="small">
                    <TableBody>
                      {attachments.data?.map((file) => (
                        <TableRow key={file.id}>
                          <TableCell><Chip label={enumLabel('ShipmentAttachmentType', file.attachmentType, language)} /></TableCell>
                          <TableCell>{file.originalFileName}</TableCell>
                          <TableCell>{formatNumber(file.sizeBytes / 1024, 0)} KB</TableCell>
                          <TableCell>{formatDateTime(file.createdAtUtc)}</TableCell>
                          <TableCell align="left">
                            <IconButton size="small" onClick={() => void download(file.downloadUrl, file.originalFileName)}><DownloadOutlinedIcon fontSize="small" /></IconButton>
                            {hasPermission(P.Shipments.Update) && <IconButton size="small" color="error" onClick={() => void act(() => shipmentsApi.deleteAttachment(id, file.id)).catch(notify.error)}><DeleteOutlineIcon fontSize="small" /></IconButton>}
                          </TableCell>
                        </TableRow>
                      ))}
                      {attachments.data?.length === 0 && <TableRow><TableCell>{t('common.noData')}</TableCell></TableRow>}
                    </TableBody>
                  </Table>
                </>
              )}
            </Box>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, lg: 4 }}>
          <Section title={tx('المبالغ', 'Amounts')}>
            <Table size="small">
              <TableBody>
                {[
                  s.pricingBasis === 'Volume'
                    ? [tx('الحجم المحتسب', 'Chargeable volume'), `${formatQty(s.totalVolumeCbm)} ${tx('م³', 'm³')}`]
                    : [tx('الوزن المحتسب', 'Chargeable weight'), `${formatQty(s.chargeableWeight)} ${tx('كغ', 'kg')}`],
                  [tx('القيمة المصرّح بها', 'Declared value'), money(s.declaredValue)],
                  [tx('أجر الشحن', 'Shipping charge'), money(s.shippingCharge)],
                  [tx('الخصم', 'Discount'), money(s.discountAmount)],
                  [tx('الضريبة', 'Tax'), money(s.taxAmount)],
                  [t('common.total'), money(s.totalCharge)],
                  [tx('يدفعها', 'Paid by'), enumLabel('ChargesPayer', s.chargesPaidBy, language)]
                ].map(([label, value]) => (
                  <TableRow key={label}><TableCell sx={{ color: 'text.secondary' }}>{label}</TableCell><TableCell align="left" sx={{ fontVariantNumeric: 'tabular-nums' }}>{value}</TableCell></TableRow>
                ))}
              </TableBody>
            </Table>
            {s.invoiceId && <Button sx={{ mt: 1 }} onClick={() => navigate(`/invoices/${s.invoiceId}`)}>{tx('الفاتورة', 'Invoice')} {s.invoiceNumber ?? ''}</Button>}
          </Section>
          {s.isCOD && (
            <Section title={tx('تحصيل ثمن البضاعة للمرسل (COD)', 'Goods value collected for the sender (COD)')}>
              <InfoGrid items={[
                { label: tx('المبلغ المطلوب', 'Expected'), value: money(s.codAmount), span: 6 },
                { label: tx('المحصّل', 'Collected'), value: s.codCollectedAmount === null ? '-' : money(s.codCollectedAmount), span: 6 },
                { label: t('common.status'), value: <StatusChip enumName="CODStatus" value={s.codStatus} />, span: 12 }
              ]} />
            </Section>
          )}
        </Grid>
      </Grid>

      {dialog === 'status' && <ChangeStatusDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={(v) => act(() => shipmentsApi.changeStatus(id, v))} />}
      {dialog === 'correct' && <CorrectStatusDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={(v) => act(() => shipmentsApi.correctStatus(id, v))} />}
      {dialog === 'driver' && <AssignDriverDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={(v) => act(() => shipmentsApi.assignDriver(id, v))} />}
      {dialog === 'invoice' && (
        <InvoiceFromShipmentDialog open shipment={s} onClose={() => setDialog(null)} onSubmit={async (v) => {
          const invoice = await api.post<InvoiceDto>(`/invoices/from-shipment/${id}`, v);
          notify.success(t('common.saved'));
          navigate(`/invoices/${invoice.id}`);
        }} />
      )}
      {dialog === 'deliver' && (
        <DeliverDialog open shipmentId={id} trackingNumber={s.trackingNumber} defaultRecipient={s.recipientName} codAmount={s.isCOD ? s.codAmount : 0} chargesAmount={s.chargesPaidBy === 'Recipient' ? s.totalCharge : 0} currencyCode={s.currencyCode}
          onClose={() => setDialog(null)} onDelivered={() => { notify.success(tx('تم تأكيد التسليم', 'Delivery confirmed')); void refresh(); }} />
      )}
      <ReasonDialog open={dialog === 'failed'} title={tx('فشل التسليم', 'Failed delivery')} label={tx('سبب الفشل', 'Reason')} onClose={() => setDialog(null)}
        onSubmit={(reason) => void act(() => shipmentsApi.deliveryFailed(id, { reason })).catch(notify.error)} />
      <ReasonDialog open={dialog === 'cancel'} danger title={tx('إلغاء الشحنة', 'Cancel shipment')} confirmText={tx('إلغاء الشحنة', 'Cancel shipment')} onClose={() => setDialog(null)}
        onSubmit={(reason) => void act(() => shipmentsApi.cancel(id, { reason })).catch(notify.error)} />
    </>
  );
}
