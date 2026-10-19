import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, CircularProgress, Grid2 as Grid, Stack, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material';
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { SenderSettlementDto } from '@/api/generated';
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
import FormDialog from '@/components/form/FormDialog';
import { useLanguage, useTx } from '@/i18n';
import { formatDateTime, formatMoney, localInputToUtc } from '@/utils/format';

/** Settlement statement: Draft -> Approved -> Paid (cash payment voucher or bank); cancel / reverse with a reason. */
export default function SettlementDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [dialog, setDialog] = useState<'pay' | 'cancel' | 'reverse' | null>(null);
  const settlement = useQuery({ queryKey: ['/cod/settlements', id], queryFn: () => api.get<SenderSettlementDto>(`/cod/settlements/${id}`) });

  const run = async (action: () => Promise<unknown>) => {
    try {
      await action();
      notify.success(t('common.done'));
      setDialog(null);
      await queryClient.invalidateQueries({ queryKey: ['/cod'] });
      await queryClient.invalidateQueries({ queryKey: ['/cod/settlements'] });
    } catch (error) {
      notify.error(error);
    }
  };

  if (settlement.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (!settlement.data) return <ErrorAlert error={settlement.error} />;
  const s = settlement.data;
  const m = (value: number) => formatMoney(value, s.currencyCode, language);

  return (
    <>
      <PageHeader back="/cod/settlements" title={<Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}><span>{s.settlementNumber}</span><StatusChip enumName="SettlementStatus" value={s.status} /></Stack>}
        subtitle={`${s.senderCode} - ${s.senderName} · ${s.branchName}`}
        actions={
          <>
            <PrintButton url={`/cod/settlements/${id}/pdf`} label={tx('كشف التسوية', 'Statement')} icon={<PictureAsPdfOutlinedIcon />} />
            {s.status === 'Draft' && hasPermission(P.Cod.ApproveSettlement) && <Button variant="contained" onClick={() => void run(() => api.post(`/cod/settlements/${id}/approve`, { notes: null }))}>{t('common.approve')}</Button>}
            {s.status === 'Approved' && hasPermission(P.Cod.PaySettlement) && <Button variant="contained" color="success" onClick={() => setDialog('pay')}>{t('common.pay')}</Button>}
            {['Draft', 'Approved'].includes(s.status) && hasPermission(P.Cod.Settle) && <Button color="error" onClick={() => setDialog('cancel')}>{t('common.cancel')}</Button>}
            {s.status === 'Paid' && hasPermission(P.Cod.ApproveSettlement) && <Button color="error" onClick={() => setDialog('reverse')}>{t('common.reverse')}</Button>}
          </>
        } />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Section title={`${tx('الشحنات', 'Shipments')} (${s.lines.length})`} noPadding>
            <Box sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead><TableRow><TableCell>{tx('رقم التتبع', 'Tracking no.')}</TableCell><TableCell>{tx('المستلم', 'Recipient')}</TableCell><TableCell>{tx('المحصّل', 'Collected')}</TableCell><TableCell>{tx('أجر الشحن', 'Shipping')}</TableCell><TableCell>{tx('العمولة', 'Commission')}</TableCell><TableCell>{tx('الضريبة', 'Tax')}</TableCell><TableCell>{tx('الصافي', 'Net')}</TableCell></TableRow></TableHead>
                <TableBody>
                  {s.lines.map((line) => (
                    <TableRow key={line.id}><TableCell dir="ltr">{line.trackingNumber}</TableCell><TableCell>{line.recipientName}</TableCell><TableCell>{m(line.collectedAmount)}</TableCell><TableCell>{m(line.shippingCharge)}</TableCell><TableCell>{m(line.codCommission)}</TableCell><TableCell>{m(line.tax)}</TableCell><TableCell><strong>{m(line.amount)}</strong></TableCell></TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          </Section>
        </Grid>
        <Grid size={{ xs: 12, lg: 4 }}>
          <Section title={tx('الملخص', 'Summary')}>
            <InfoGrid items={[
              { label: tx('الإجمالي المحرر', 'Gross released'), value: m(s.totalGrossReleased), span: 6 },
              { label: tx('أجور الشحن', 'Shipping charges'), value: m(s.totalShippingCharges), span: 6 },
              { label: tx('العمولات', 'Commissions'), value: m(s.totalCodCommission), span: 6 },
              { label: tx('الضريبة', 'Tax'), value: m(s.totalTax), span: 6 },
              { label: tx('رسوم أخرى', 'Other fees'), value: m(s.totalOtherFees), span: 6 },
              { label: tx('تعويضات', 'Compensation'), value: m(s.totalCompensation), span: 6 },
              { label: tx('الصافي المستحق', 'Net payable'), value: <strong>{m(s.netPayable)}</strong>, span: 12 },
              { label: tx('طريقة الدفع', 'Paid by'), value: [s.paymentMethodName, s.cashboxName ?? s.bankAccountName, s.paymentReference].filter(Boolean).join(' · '), span: 12 },
              { label: tx('سند الصرف', 'Payment voucher'), value: s.cashPaymentVoucherNumber, span: 6 },
              { label: tx('دُفعت', 'Paid at'), value: formatDateTime(s.paidAtUtc), span: 6 },
              { label: t('common.notes'), value: s.notes ?? s.cancellationReason ?? s.reversalReason, span: 12 }
            ]} />
          </Section>
        </Grid>
      </Grid>
      <FormDialog<Record<string, unknown>>
        open={dialog === 'pay'} maxWidth="sm" title={`${t('common.pay')} ${m(s.netPayable)}`}
        fields={[
          { name: 'paymentMethodId', label: tx('طريقة الدفع', 'Payment method'), type: 'lookup', lookup: 'paymentMethods', required: true, span: 12 },
          { name: 'cashboxId', label: tx('من صندوق (نقداً)', 'From cashbox (cash)'), type: 'lookup', lookup: 'cashboxes', disabled: (v) => !!v.bankAccountId },
          { name: 'bankAccountId', label: tx('أو من حساب بنكي', 'Or from bank account'), type: 'lookup', lookup: 'bankAccounts', disabled: (v) => !!v.cashboxId },
          { name: 'referenceNumber', label: tx('المرجع', 'Reference') },
          { name: 'paidAt', label: tx('وقت الدفع', 'Paid at'), type: 'datetime' }
        ]}
        defaultValues={{ paymentMethodId: null, cashboxId: null, bankAccountId: null, referenceNumber: null, paidAt: null }}
        onClose={() => setDialog(null)}
        onSubmit={async ({ paidAt, ...values }) => run(() => api.post(`/cod/settlements/${id}/pay`, { ...values, paidAtUtc: localInputToUtc(paidAt as string | null) }))}
      />
      <ReasonDialog open={dialog === 'cancel'} danger title={tx('إلغاء التسوية', 'Cancel settlement')} onClose={() => setDialog(null)} onSubmit={(reason) => void run(() => api.post(`/cod/settlements/${id}/cancel`, { reason }))} />
      <ReasonDialog open={dialog === 'reverse'} danger title={tx('عكس التسوية المدفوعة', 'Reverse the paid settlement')} onClose={() => setDialog(null)} onSubmit={(reason) => void run(() => api.post(`/cod/settlements/${id}/reverse`, { reason }))} />
    </>
  );
}
