import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, CircularProgress, Grid2 as Grid, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import SendOutlinedIcon from '@mui/icons-material/SendOutlined';
import PaymentsOutlinedIcon from '@mui/icons-material/PaymentsOutlined';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { InvoiceDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import StatusChip from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { PrintButton } from '@/components/common/ExportMenu';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import { useLanguage, useTx } from '@/i18n';
import { formatDate, formatDateTime, formatMoney, formatNumber } from '@/utils/format';

/** Invoice: lines (one per shipment), payments, credit notes; issue, cancel, credit note, PDF, record a payment. */
export default function InvoiceDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [dialog, setDialog] = useState<'cancel' | 'credit' | null>(null);
  const [busy, setBusy] = useState(false);
  const invoice = useQuery({ queryKey: ['/invoices', id], queryFn: () => api.get<InvoiceDto>(`/invoices/${id}`) });

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await action();
      notify.success(t('common.done'));
      setDialog(null);
      await queryClient.invalidateQueries({ queryKey: ['/invoices'] });
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  if (invoice.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (!invoice.data) return <ErrorAlert error={invoice.error} />;
  const inv = invoice.data;
  const m = (value: number) => formatMoney(value, inv.currencyCode, language);
  const open = ['Issued', 'PartiallyPaid', 'Overdue'].includes(inv.status);

  return (
    <>
      <PageHeader back="/invoices" title={<Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}><span>{inv.invoiceNumber ?? tx('مسودة فاتورة', 'Draft invoice')}</span><StatusChip enumName="InvoiceStatus" value={inv.status} /></Stack>}
        subtitle={`${inv.customerCode} - ${inv.customerName} · ${inv.branchName}`}
        actions={
          <>
            {inv.status !== 'Draft' && hasPermission(P.Invoices.Export) && <PrintButton url={`/invoices/${id}/pdf`} label="PDF" icon={<PictureAsPdfOutlinedIcon />} />}
            {inv.status === 'Draft' && hasPermission(P.Invoices.Issue) && <Button variant="contained" startIcon={<SendOutlinedIcon />} disabled={busy} onClick={() => void run(() => api.post(`/invoices/${id}/issue`, {}))}>{tx('إصدار', 'Issue')}</Button>}
            {open && inv.balanceDue > 0 && hasPermission(P.Payments.Create) && <Button variant="contained" color="success" startIcon={<PaymentsOutlinedIcon />} onClick={() => navigate(`/payments?new=1&customerId=${inv.customerId}&invoiceId=${id}&amount=${inv.balanceDue}&currencyId=${inv.currencyId}`)}>{tx('تسجيل دفعة', 'Record payment')}</Button>}
            {inv.status !== 'Draft' && inv.status !== 'Cancelled' && hasPermission(P.Invoices.CreditNotes) && <Button onClick={() => setDialog('credit')}>{tx('إشعار دائن', 'Credit note')}</Button>}
            {['Draft', 'Issued'].includes(inv.status) && inv.paidAmount === 0 && hasPermission(P.Invoices.Cancel) && <Button color="error" onClick={() => setDialog('cancel')}>{tx('إلغاء', 'Cancel')}</Button>}
          </>
        } />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Section title={`${tx('البنود', 'Lines')} (${inv.lines.length})`} noPadding>
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead><TableRow><TableCell>#</TableCell><TableCell>{tx('الشحنة', 'Shipment')}</TableCell><TableCell>{tx('البيان', 'Description')}</TableCell><TableCell>{tx('الأجر', 'Charge')}</TableCell><TableCell>{tx('الخصم', 'Discount')}</TableCell><TableCell>{tx('الضريبة', 'Tax')}</TableCell><TableCell>{t('common.total')}</TableCell></TableRow></TableHead>
                <TableBody>
                  {inv.lines.map((line) => (
                    <TableRow key={line.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/shipments/${line.shipmentId}`)}>
                      <TableCell>{line.lineNumber}</TableCell>
                      <TableCell dir="ltr">{line.trackingNumber}</TableCell>
                      <TableCell>{line.description}</TableCell>
                      <TableCell>{m(line.shippingCharge)}</TableCell>
                      <TableCell>{m(line.lineDiscountAmount + line.invoiceDiscountAmount)}</TableCell>
                      <TableCell>{m(line.taxAmount)} <Typography component="span" variant="caption" color="text.secondary">({formatNumber(line.taxPercentage, 2)}%)</Typography></TableCell>
                      <TableCell>{m(line.lineTotal)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          </Section>
          {inv.payments.length > 0 && (
            <Section title={tx('الدفعات', 'Payments')} noPadding>
              <Table size="small"><TableBody>
                {inv.payments.map((p) => <TableRow key={p.paymentId}><TableCell>{p.paymentNumber}</TableCell><TableCell>{p.paymentMethodName}</TableCell><TableCell>{formatDateTime(p.allocatedAtUtc)}</TableCell><TableCell>{m(p.amount)}</TableCell></TableRow>)}
              </TableBody></Table>
            </Section>
          )}
          {inv.creditNotes.length > 0 && (
            <Section title={tx('الإشعارات الدائنة', 'Credit notes')} noPadding>
              <Table size="small"><TableBody>
                {inv.creditNotes.map((c) => <TableRow key={c.id}><TableCell>{c.creditNoteNumber}</TableCell><TableCell>{formatDate(c.creditNoteDateUtc)}</TableCell><TableCell>{c.reason}</TableCell><TableCell>{m(c.totalAmount)}</TableCell></TableRow>)}
              </TableBody></Table>
            </Section>
          )}
        </Grid>
        <Grid size={{ xs: 12, lg: 4 }}>
          <Section title={tx('الملخص', 'Summary')}>
            <InfoGrid items={[
              { label: tx('تاريخ الفاتورة', 'Invoice date'), value: formatDate(inv.invoiceDateUtc), span: 6 },
              { label: tx('الاستحقاق', 'Due date'), value: formatDate(inv.dueDateUtc), span: 6 },
              { label: tx('المجموع', 'Subtotal'), value: m(inv.subTotal), span: 6 },
              { label: tx('الخصم', 'Discount'), value: m(inv.discountAmount), span: 6 },
              { label: tx('الضريبة', 'Tax'), value: m(inv.taxAmount), span: 6 },
              { label: t('common.total'), value: <strong>{m(inv.totalAmount)}</strong>, span: 6 },
              { label: tx('المدفوع', 'Paid'), value: m(inv.paidAmount), span: 6 },
              { label: tx('المُشعر دائن', 'Credited'), value: m(inv.creditedAmount), span: 6 },
              { label: tx('المتبقي', 'Balance due'), value: <strong>{m(inv.balanceDue)}</strong>, span: 12 },
              { label: t('common.notes'), value: inv.notes ?? inv.cancellationReason, span: 12 }
            ]} />
          </Section>
        </Grid>
      </Grid>
      <ReasonDialog open={dialog === 'cancel'} danger busy={busy} title={tx('إلغاء الفاتورة', 'Cancel invoice')} onClose={() => setDialog(null)} onSubmit={(reason) => void run(() => api.post(`/invoices/${id}/cancel`, { reason }))} />
      <ReasonDialog open={dialog === 'credit'} busy={busy} title={tx('إشعار دائن بكامل الرصيد القابل للإشعار', 'Credit note for the full creditable amount')} onClose={() => setDialog(null)} onSubmit={(reason) => void run(() => api.post(`/invoices/${id}/credit-notes`, { reason, creditAll: true, lines: [] }))} />
    </>
  );
}
