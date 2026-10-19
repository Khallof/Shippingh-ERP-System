import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, Card, CircularProgress, IconButton, Tab, Tabs, Tooltip } from '@mui/material';
import LockClockOutlinedIcon from '@mui/icons-material/LockClockOutlined';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import type { GridColDef } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { CashboxDailyClosingDto, CashboxDto, CashboxTransactionDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import { ActiveChip } from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import DataTable from '@/components/data/DataTable';
import FilterBar from '@/components/data/FilterBar';
import { col } from '@/components/data/columns';
import { useApiList } from '@/components/data/useApiList';
import { useListState } from '@/components/data/useListState';
import FormDialog from '@/components/form/FormDialog';
import { useLanguage, useTx } from '@/i18n';
import { businessToday, formatDateTime, formatMoney } from '@/utils/format';

/** Cashbox ledger (every movement with the running balance) and daily closings (count, discrepancy, approval). */
export default function CashboxDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [tab, setTab] = useState(0);
  const [closing, setClosing] = useState(false);
  const [decision, setDecision] = useState<{ row: CashboxDailyClosingDto; approve: boolean } | null>(null);

  const cashbox = useQuery({ queryKey: ['/cashboxes', id], queryFn: () => api.get<CashboxDto>(`/cashboxes/${id}`) });
  const state = useListState({ prefix: tab === 0 ? 't_' : 'c_' });
  const transactions = useApiList<CashboxTransactionDto>(`/cashboxes/${id}/transactions`, state.params, { enabled: tab === 0 });
  const closings = useApiList<CashboxDailyClosingDto>(`/cashboxes/${id}/daily-closings`, state.params, { enabled: tab === 1 });

  if (cashbox.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (!cashbox.data) return <ErrorAlert error={cashbox.error} />;
  const c = cashbox.data;
  const m = (value: number | null | undefined) => formatMoney(value, c.currencyCode, language);

  const txColumns: GridColDef<CashboxTransactionDto>[] = [
    col.dateTime('transactionDateUtc', t('common.date')),
    col.text('transactionTypeName', tx('الحركة', 'Movement')),
    col.text('documentNumber', tx('المستند', 'Document')),
    col.text('counterpartyName', tx('الطرف', 'Counterparty')),
    col.text('description', tx('البيان', 'Description'), { minWidth: 200 }),
    col.money('amountIn', tx('وارد', 'In')),
    col.money('amountOut', tx('صادر', 'Out')),
    col.money('balanceAfter', tx('الرصيد', 'Balance'))
  ];
  const closingColumns: GridColDef<CashboxDailyClosingDto>[] = [
    col.date('businessDate', tx('اليوم', 'Day')),
    col.money('openingBalance', tx('الافتتاحي', 'Opening')),
    col.money('totalIn', tx('الوارد', 'In')),
    col.money('totalOut', tx('الصادر', 'Out')),
    col.money('expectedBalance', tx('المتوقع', 'Expected')),
    col.money('countedBalance', tx('المعدود', 'Counted')),
    col.money('discrepancy', tx('الفرق', 'Difference')),
    col.status('status', t('common.status'), 'CashboxClosingStatus'),
    col.text('discrepancyReason', t('common.reason')),
    {
      field: '__actions', headerName: '', width: 100, renderCell: ({ row }) => row.status === 'PendingApproval' && hasPermission(P.Cashboxes.ApproveClosing) ? (
        <>
          <Tooltip title={t('common.approve')}><IconButton size="small" color="success" onClick={() => setDecision({ row, approve: true })}><CheckIcon fontSize="small" /></IconButton></Tooltip>
          <Tooltip title={t('common.reject')}><IconButton size="small" color="error" onClick={() => setDecision({ row, approve: false })}><CloseIcon fontSize="small" /></IconButton></Tooltip>
        </>
      ) : null
    }
  ];

  return (
    <>
      <PageHeader back="/cash/cashboxes" title={`${c.code} - ${c.arabicName}`} subtitle={`${c.branchName} · ${c.currencyCode}`}
        actions={hasPermission(P.Cashboxes.Close) && <Button variant="contained" startIcon={<LockClockOutlinedIcon />} onClick={() => setClosing(true)}>{tx('إقفال يومي', 'Close the day')}</Button>} />
      <Section>
        <InfoGrid items={[
          { label: tx('الرصيد الحالي', 'Current balance'), value: <strong>{m(c.currentBalance)}</strong> },
          { label: tx('أمين الصندوق', 'Custodian'), value: c.custodianName },
          { label: tx('آخر حركة', 'Last movement'), value: formatDateTime(c.lastTransactionAtUtc) },
          { label: t('common.status'), value: <ActiveChip active={c.isActive} /> }
        ]} />
      </Section>
      <Card sx={{ mb: 2 }}>
        <Tabs value={tab} onChange={(_, v: number) => setTab(v)}>
          <Tab label={tx('دفتر الصندوق', 'Cash ledger')} />
          <Tab label={tx('الإقفالات اليومية', 'Daily closings')} />
        </Tabs>
      </Card>
      <FilterBar key={tab} state={state} filters={[{ name: 'fromDate', label: t('common.fromDate'), type: 'date' }, { name: 'toDate', label: t('common.toDate'), type: 'date' }]} />
      {tab === 0
        ? <DataTable<CashboxTransactionDto> columns={txColumns} data={transactions.data} loading={transactions.isFetching} error={transactions.error} state={state} />
        : <DataTable<CashboxDailyClosingDto> columns={closingColumns} data={closings.data} loading={closings.isFetching} error={closings.error} state={state} />}

      <FormDialog<Record<string, unknown>>
        open={closing} maxWidth="sm" title={`${tx('إقفال يومي', 'Daily closing')} · ${tx('الرصيد الدفتري', 'Book balance')} ${m(c.currentBalance)}`}
        fields={[
          { name: 'businessDate', label: tx('اليوم', 'Day'), type: 'date', required: true },
          { name: 'countedBalance', label: tx('المبلغ المعدود فعلاً', 'Counted cash'), type: 'money', required: true, min: 0 },
          { name: 'discrepancyReason', label: tx('سبب الفرق (مطلوب عند وجود فرق)', 'Reason of the difference (required if any)'), span: 12 },
          { name: 'notes', label: t('common.notes'), type: 'textarea' }
        ]}
        defaultValues={{ businessDate: businessToday(), countedBalance: c.currentBalance, discrepancyReason: null, notes: null }}
        onClose={() => setClosing(false)}
        onSubmit={async (values) => {
          await api.post(`/cashboxes/${id}/daily-closings`, values);
          notify.success(t('common.done'));
          setClosing(false);
          setTab(1);
          await queryClient.invalidateQueries({ queryKey: ['/cashboxes'] });
          await queryClient.invalidateQueries({ queryKey: [`/cashboxes/${id}/daily-closings`] });
        }}
      />
      <ReasonDialog open={!!decision} required={false} title={decision?.approve ? t('common.approve') : t('common.reject')} label={t('common.notes')} danger={!decision?.approve}
        onClose={() => setDecision(null)}
        onSubmit={async (notes) => {
          try {
            await api.post(`/cashboxes/daily-closings/${decision!.row.id}/${decision!.approve ? 'approve' : 'reject'}`, { notes: notes || null });
            setDecision(null);
            notify.success(t('common.done'));
            await queryClient.invalidateQueries({ queryKey: [`/cashboxes/${id}/daily-closings`] });
            await queryClient.invalidateQueries({ queryKey: ['/cashboxes'] });
          } catch (error) {
            notify.error(error);
          }
        }} />
    </>
  );
}
