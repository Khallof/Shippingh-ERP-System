import { useCallback, useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, Card, CardContent, CardHeader, Divider, Grid2 as Grid, IconButton, Table, TableBody, TableCell, TableHead, TableRow, Tooltip } from '@mui/material';
import GavelOutlinedIcon from '@mui/icons-material/GavelOutlined';
import HandshakeOutlinedIcon from '@mui/icons-material/HandshakeOutlined';
import PointOfSaleOutlinedIcon from '@mui/icons-material/PointOfSaleOutlined';
import type { GridColDef } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api, idempotent, newIdempotencyKey } from '@/api/client';
import type { CodAgingReportDto, CodDashboardDto, CodTransactionListItemDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import StatCard from '@/components/common/StatCard';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import { CategoryBarChart } from '@/components/charts/Charts';
import DataTable from '@/components/data/DataTable';
import FilterBar from '@/components/data/FilterBar';
import { col, S } from '@/components/data/columns';
import { useApiList } from '@/components/data/useApiList';
import { useListState } from '@/components/data/useListState';
import FormDialog from '@/components/form/FormDialog';
import { enumLabel } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { formatMoney, formatNumber } from '@/utils/format';

/**
 * COD dashboard: where the cash of delivered shipments is (stages), aging by sender, the COD transaction list
 * (mark ready for settlement, open a dispute) and counter collection (recipient pays at the branch).
 */
export default function CodPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const state = useListState({ defaultSort: 'createdAtUtc' });
  const list = useApiList<CodTransactionListItemDto>('/cod', state.params);
  const [selected, setSelected] = useState<number[]>([]);
  const [dispute, setDispute] = useState<CodTransactionListItemDto | null>(null);
  const [collect, setCollect] = useState<string | null>(null);

  const dashboard = useQuery({ queryKey: ['/cod', 'dashboard'], queryFn: () => api.get<CodDashboardDto>('/cod/dashboard') });
  const aging = useQuery({ queryKey: ['/cod', 'aging'], queryFn: () => api.get<CodAgingReportDto>('/cod/aging') });
  const d = dashboard.data;
  const money = useCallback((value: number | undefined) => formatMoney(value ?? 0, d?.currencyCode, language), [d?.currencyCode, language]);
  const stageData = useMemo(() => (d?.stages ?? []).map((s) => ({ label: enumLabel('CODStatus', s.status, language), amount: s.amount, count: s.count })), [d, language]);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['/cod'] });
  const run = async (action: () => Promise<unknown>) => {
    try {
      await action();
      notify.success(t('common.done'));
      await refresh();
    } catch (error) {
      notify.error(error);
    }
  };

  const columns: GridColDef<CodTransactionListItemDto>[] = [
    col.text('trackingNumber', tx('رقم التتبع', 'Tracking no.'), { minWidth: 140 }),
    col.status('status', t('common.status'), 'CODStatus', S),
    col.text('senderName', tx('المرسل', 'Sender'), { minWidth: 150 }),
    col.text('driverName', t('common.driver')),
    col.money('expectedAmount', tx('المطلوب', 'Expected'), 'currencyCode', S),
    col.money('collectedAmount', tx('المحصّل', 'Collected'), 'currencyCode', S),
    col.money('netPayableAmount', tx('صافي المستحق للمرسل', 'Net payable'), 'currencyCode', S),
    col.money('remainingPayable', tx('المتبقي', 'Remaining'), 'currencyCode'),
    col.dateTime('collectedAtUtc', tx('وقت التحصيل', 'Collected at'), S),
    {
      field: '__actions', headerName: '', width: 60, renderCell: ({ row }) => hasPermission(P.Cod.Adjust) && !['Disputed', 'Cancelled', 'SettledToSender'].includes(row.status) ? (
        <Tooltip title={tx('فتح نزاع', 'Open dispute')}><IconButton size="small" onClick={() => setDispute(row)}><GavelOutlinedIcon fontSize="small" /></IconButton></Tooltip>
      ) : null
    }
  ];

  return (
    <>
      <PageHeader title={t('nav.codDashboard')} subtitle={d ? `${tx('العملة', 'Currency')} ${d.currencyCode}` : undefined}
        actions={
          <>
            {hasPermission(P.Cod.Collect) && <Button variant="outlined" startIcon={<PointOfSaleOutlinedIcon />} onClick={() => setCollect(newIdempotencyKey())}>{tx('تحصيل في الفرع', 'Counter collection')}</Button>}
            {hasPermission(P.Cod.Settle) && <Button variant="contained" startIcon={<HandshakeOutlinedIcon />} disabled={selected.length === 0} onClick={() => void run(() => api.post('/cod/mark-ready', { codTransactionIds: selected }))}>{tx('جاهز للتسوية', 'Mark ready')} ({selected.length})</Button>}
          </>
        } />
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 6, md: 3 }}><StatCard loading={dashboard.isLoading} tone="warning" label={tx('مُسلّمة ولم تُحصّل', 'Delivered, not collected')} value={money(d?.deliveredNotCollectedAmount)} hint={`${formatNumber(d?.deliveredNotCollectedCount)} ${tx('شحنة', 'shipments')}`} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard loading={dashboard.isLoading} label={tx('عهد السائقين', 'Held by drivers')} value={money(d?.driversOutstandingTotal)} hint={`${formatNumber(d?.driversWithBalance)} ${tx('سائق', 'drivers')}`} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard loading={dashboard.isLoading} label={tx('في الصناديق', 'In cashboxes')} value={money(d?.cashboxBalanceTotal)} hint={`${tx('تحويلات في الطريق', 'In transit')} ${money(d?.inTransitTransfersAmount)}`} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><StatCard loading={dashboard.isLoading} tone="good" label={tx('صافي مستحق للمرسلين', 'Net due to senders')} value={money(d?.unsettledSenderNet)} hint={`${formatNumber(d?.openSettlementsCount)} ${tx('تسوية مفتوحة', 'open settlements')}`} /></Grid>
      </Grid>
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, md: 5 }}>
          <Card sx={{ height: '100%' }}>
            <CardHeader title={tx('المبالغ حسب المرحلة', 'Amount by stage')} titleTypographyProps={{ variant: 'subtitle1', fontWeight: 700 }} />
            <Divider />
            <CardContent>
              {d && <CategoryBarChart data={stageData} labelKey="label" valueKey="amount" valueName={t('common.amount')} formatValue={money} colorIndex={2} />}
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 7 }}>
          <Card sx={{ height: '100%' }}>
            <CardHeader title={tx('أعمار المبالغ حسب المرسل', 'COD aging by sender')} titleTypographyProps={{ variant: 'subtitle1', fontWeight: 700 }} />
            <Divider />
            <Box sx={{ overflowX: 'auto', maxHeight: 340 }}>
              <Table size="small" stickyHeader>
                <TableHead><TableRow><TableCell>{tx('المرسل', 'Sender')}</TableCell><TableCell>0-7</TableCell><TableCell>8-15</TableCell><TableCell>16-30</TableCell><TableCell>31-60</TableCell><TableCell>60+</TableCell><TableCell>{t('common.total')}</TableCell></TableRow></TableHead>
                <TableBody>
                  {aging.data?.rows.map((row) => (
                    <TableRow key={row.senderCustomerId}>
                      <TableCell>{row.senderName}</TableCell>
                      {[row.days0To7, row.days8To15, row.days16To30, row.days31To60, row.over60Days].map((v, i) => <TableCell key={i} sx={{ color: i >= 3 && v > 0 ? 'error.main' : undefined }}>{formatNumber(v, 2)}</TableCell>)}
                      <TableCell><strong>{formatNumber(row.total, 2)}</strong></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Box>
          </Card>
        </Grid>
      </Grid>
      <FilterBar state={state} filters={[
        { name: 'status', label: t('common.status'), type: 'enum', enumName: 'CODStatus' },
        { name: 'senderCustomerId', label: tx('المرسل', 'Sender'), type: 'lookup', lookup: 'senders' },
        { name: 'driverId', label: t('common.driver'), type: 'lookup', lookup: 'drivers' },
        { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' }
      ]} />
      <DataTable<CodTransactionListItemDto> columns={columns} data={list.data} loading={list.isFetching} error={list.error} state={state} checkboxSelection onSelectionChange={(ids) => setSelected(ids.map(Number))} />

      <ReasonDialog open={!!dispute} title={tx('فتح نزاع على التحصيل', 'Open a COD dispute')} onClose={() => setDispute(null)}
        onSubmit={(reason) => { const row = dispute!; setDispute(null); void run(() => api.post(`/cod/${row.id}/dispute`, { reason })); }} />
      <FormDialog<Record<string, unknown>>
        open={!!collect}
        maxWidth="sm"
        title={tx('تحصيل من المستلم في الفرع', 'Collect at the counter')}
        fields={[
          { name: 'trackingNumber', label: tx('رقم التتبع', 'Tracking number'), required: true, span: 12 },
          { name: 'amount', label: tx('المبلغ المحصّل', 'Amount collected'), type: 'money', required: true, min: 0.01 },
          { name: 'cashboxId', label: tx('الصندوق', 'Cashbox'), type: 'lookup', lookup: 'cashboxes', required: true },
          { name: 'differenceReason', label: tx('سبب الفرق (إن وجد)', 'Difference reason (if any)'), span: 12 },
          { name: 'notes', label: t('common.notes'), type: 'textarea' }
        ]}
        defaultValues={{ trackingNumber: '', amount: null, cashboxId: null, differenceReason: null, notes: null }}
        onClose={() => setCollect(null)}
        onSubmit={async (values) => {
          await api.post('/cod/collect', values, idempotent(collect!));
          notify.success(t('common.done'));
          setCollect(null);
          await refresh();
        }}
      />
    </>
  );
}
