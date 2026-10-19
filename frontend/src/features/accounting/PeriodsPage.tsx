import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Box, Button, Card, CardContent, CardHeader, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { AccountingPeriodDto, FiscalYearDto, PeriodCloseCheckDto } from '@/api/generated';
import type { PagedResult } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import StatusChip from '@/components/common/StatusChip';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import FormDialog from '@/components/form/FormDialog';
import { useTx } from '@/i18n';
import { formatDate, formatDateTime } from '@/utils/format';

/** Fiscal years and monthly periods: close (after the close check), reopen with a reason, close the year. */
export default function PeriodsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission(P.Accounting.ManagePeriods);
  const [creating, setCreating] = useState(false);
  const [check, setCheck] = useState<{ period: AccountingPeriodDto; result: PeriodCloseCheckDto } | null>(null);
  const [reopen, setReopen] = useState<AccountingPeriodDto | null>(null);

  const years = useQuery({ queryKey: ['/accounting/fiscal-years'], queryFn: () => api.get<PagedResult<FiscalYearDto> | FiscalYearDto[]>('/accounting/fiscal-years', { pageSize: 20 }) });
  const yearList = Array.isArray(years.data) ? years.data : years.data?.items ?? [];
  const periods = useQuery({ queryKey: ['/accounting/periods'], queryFn: () => api.get<PagedResult<AccountingPeriodDto>>('/accounting/periods', { pageSize: 100, sortBy: 'startDate', sortDirection: 'desc' }) });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['/accounting/fiscal-years'] });
    await queryClient.invalidateQueries({ queryKey: ['/accounting/periods'] });
  };
  const run = async (action: () => Promise<unknown>) => {
    try {
      await action();
      notify.success(t('common.done'));
      await refresh();
    } catch (error) {
      notify.error(error);
    }
  };

  const startClose = async (period: AccountingPeriodDto) => {
    try {
      setCheck({ period, result: await api.get<PeriodCloseCheckDto>(`/accounting/periods/${period.id}/close-check`) });
    } catch (error) {
      notify.error(error);
    }
  };

  return (
    <>
      <PageHeader title={t('nav.fiscalPeriods')} actions={canManage && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreating(true)}>{tx('سنة مالية', 'Fiscal year')}</Button>} />
      <Stack spacing={2}>
        {yearList.map((year) => (
          <Card key={year.id}>
            <CardHeader title={`${year.code} - ${year.arabicName}`} subheader={`${formatDate(year.startDate)} → ${formatDate(year.endDate)} · ${tx('فترات مفتوحة', 'open periods')} ${year.openPeriodCount}/${year.periodCount}`}
              action={<Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><StatusChip enumName="FiscalYearStatus" value={year.status} />
                {canManage && year.status === 'Open' && <Button size="small" color="warning" onClick={() => void run(() => api.post(`/accounting/fiscal-years/${year.id}/close`))}>{tx('إقفال السنة', 'Close year')}</Button>}</Stack>} />
            <CardContent sx={{ pt: 0 }}>
              <Box sx={{ overflowX: 'auto' }}>
                <Table size="small">
                  <TableHead><TableRow><TableCell>#</TableCell><TableCell>{tx('الفترة', 'Period')}</TableCell><TableCell>{tx('من', 'From')}</TableCell><TableCell>{tx('إلى', 'To')}</TableCell><TableCell>{t('common.status')}</TableCell><TableCell>{tx('أُقفلت', 'Closed at')}</TableCell><TableCell /></TableRow></TableHead>
                  <TableBody>
                    {(periods.data?.items ?? []).filter((p) => p.fiscalYearId === year.id).sort((a, b) => a.periodNumber - b.periodNumber).map((p) => (
                      <TableRow key={p.id}>
                        <TableCell>{p.periodNumber}</TableCell><TableCell>{p.name}</TableCell><TableCell>{formatDate(p.startDate)}</TableCell><TableCell>{formatDate(p.endDate)}</TableCell>
                        <TableCell><StatusChip enumName="AccountingPeriodStatus" value={p.status} /></TableCell><TableCell>{formatDateTime(p.closedAtUtc)}</TableCell>
                        <TableCell align="left">
                          {canManage && p.status === 'Open' && <Button size="small" onClick={() => void startClose(p)}>{tx('إقفال', 'Close')}</Button>}
                          {canManage && p.status === 'Closed' && year.status === 'Open' && <Button size="small" color="warning" onClick={() => setReopen(p)}>{tx('إعادة فتح', 'Reopen')}</Button>}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Box>
            </CardContent>
          </Card>
        ))}
      </Stack>

      <Dialog open={!!check} onClose={() => setCheck(null)} maxWidth="sm" fullWidth>
        <DialogTitle>{tx('فحص إقفال الفترة', 'Period close check')} · {check?.period.name}</DialogTitle>
        <DialogContent dividers>
          {check && (
            <>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', mb: 2 }}>
                <Chip label={`${tx('قيود غير مرحلة', 'Unposted')}: ${check.result.unpostedEntries}`} />
                <Chip label={`${tx('بانتظار الاعتماد', 'Awaiting approval')}: ${check.result.entriesAwaitingApproval}`} />
                <Chip label={`${tx('أحداث ترحيل معلقة', 'Pending postings')}: ${check.result.pendingPostingEvents}`} />
                <Chip color={check.result.failedPostingEvents > 0 ? 'error' : 'default'} label={`${tx('ترحيل فاشل', 'Failed postings')}: ${check.result.failedPostingEvents}`} />
              </Stack>
              {check.result.canClose ? <Alert severity="success">{tx('يمكن إقفال الفترة. بعد الإقفال لا تُقبل قيود بتاريخ ضمنها.', 'The period can be closed. Afterwards no entry can be dated inside it.')}</Alert>
                : <Alert severity="error"><ul style={{ margin: 0 }}>{check.result.blockers.map((b) => <li key={b}>{b}</li>)}</ul></Alert>}
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCheck(null)}>{t('common.cancel')}</Button>
          <Button variant="contained" disabled={!check?.result.canClose} onClick={() => { const p = check!.period; setCheck(null); void run(() => api.post(`/accounting/periods/${p.id}/close`, { notes: null })); }}>{tx('إقفال الفترة', 'Close period')}</Button>
        </DialogActions>
      </Dialog>
      <ReasonDialog open={!!reopen} title={`${tx('إعادة فتح', 'Reopen')} ${reopen?.name ?? ''}`} onClose={() => setReopen(null)} onSubmit={(reason) => { const p = reopen!; setReopen(null); void run(() => api.post(`/accounting/periods/${p.id}/reopen`, { reason })); }} />
      <FormDialog<Record<string, unknown>>
        open={creating} maxWidth="sm" title={tx('سنة مالية جديدة (تُنشأ 12 فترة شهرية)', 'New fiscal year (12 monthly periods)')}
        fields={[
          { name: 'startDate', label: tx('تاريخ البداية', 'Start date'), type: 'date', required: true },
          { name: 'endDate', label: tx('تاريخ النهاية (اختياري)', 'End date (optional)'), type: 'date' },
          { name: 'code', label: tx('الرمز (تلقائي)', 'Code (auto)') },
          { name: 'arabicName', label: tx('الاسم (تلقائي)', 'Name (auto)') }
        ]}
        defaultValues={{ startDate: null, endDate: null, code: null, arabicName: null }}
        onClose={() => setCreating(false)}
        onSubmit={async (values) => { await api.post('/accounting/fiscal-years', values); setCreating(false); notify.success(t('common.saved')); await refresh(); }}
      />
    </>
  );
}
