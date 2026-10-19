import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Box, Card, CardContent, CardHeader, Divider, Grid2 as Grid, LinearProgress, MenuItem, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { OperationalGroupRowDto, OperationalReportDto } from '@/api/generated';
import PageHeader from '@/components/common/PageHeader';
import StatCard from '@/components/common/StatCard';
import ErrorAlert from '@/components/common/ErrorAlert';
import ExportMenu from '@/components/common/ExportMenu';
import { CategoryBarChart, TrendChart } from '@/components/charts/Charts';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { businessDaysAgo, businessToday, formatMoney, formatNumber, formatPercent } from '@/utils/format';
import ReportFilters, { type ReportFilterValues } from './ReportFilters';

function GroupTable({ rows }: { rows: OperationalGroupRowDto[] }) {
  const tx = useTx();
  const language = useLanguage();
  return (
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small">
        <TableHead><TableRow><TableCell>{tx('الاسم', 'Name')}</TableCell><TableCell>{tx('الشحنات', 'Total')}</TableCell><TableCell>{tx('سُلّمت', 'Delivered')}</TableCell><TableCell>{tx('فشلت', 'Failed')}</TableCell><TableCell>{tx('قيد التنفيذ', 'In progress')}</TableCell><TableCell>{tx('ملغاة', 'Cancelled')}</TableCell><TableCell>{tx('نسبة التسليم', 'Delivered %')}</TableCell><TableCell>{tx('الأجور', 'Charges')}</TableCell><TableCell>COD</TableCell></TableRow></TableHead>
        <TableBody>
          {rows.map((r, i) => (
            <TableRow key={`${r.id ?? 'x'}-${i}`}>
              <TableCell>{r.name}</TableCell><TableCell>{formatNumber(r.total)}</TableCell><TableCell>{formatNumber(r.delivered)}</TableCell><TableCell>{formatNumber(r.failed)}</TableCell>
              <TableCell>{formatNumber(r.inProgress)}</TableCell><TableCell>{formatNumber(r.cancelled)}</TableCell><TableCell>{formatPercent(r.deliveredPercent, language)}</TableCell>
              <TableCell>{formatMoney(r.totalCharges)}</TableCell><TableCell>{formatMoney(r.codAmount)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

/** Operational report of the shipments created in a period: KPIs, daily trend, status mix and breakdowns. */
export default function OperationalReportsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const [values, setValues] = useState<ReportFilterValues>({ fromDate: businessDaysAgo(29), toDate: businessToday(), branchId: null, senderCustomerId: null, driverId: null, status: null });
  const [tab, setTab] = useState(0);
  const report = useQuery({ queryKey: ['/reports/operations', values], queryFn: () => api.get<OperationalReportDto>('/reports/operations', values), placeholderData: keepPreviousData });
  const d = report.data;
  const groups = d ? [d.byBranch, d.byDriver, d.byCustomer, d.byServiceType] : [];

  return (
    <>
      <PageHeader title={t('nav.operationalReports')} />
      <ReportFilters values={values} onChange={setValues} lookups={[
        { name: 'branchId', label: t('common.branch'), source: 'branches' },
        { name: 'senderCustomerId', label: tx('المرسل', 'Sender'), source: 'senders' },
        { name: 'driverId', label: t('common.driver'), source: 'drivers' }
      ]}>
        <TextField select label={t('common.status')} value={values.status ?? ''} onChange={(e) => setValues({ ...values, status: e.target.value || null })} sx={{ width: 170 }}>
          <MenuItem value="">{t('common.all')}</MenuItem>
          {enumOptions('ShipmentStatus', language).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
        </TextField>
        <ExportMenu url="/reports/operations/export" params={values} />
      </ReportFilters>
      {report.isFetching && <LinearProgress sx={{ mb: 1 }} />}
      {report.error && <ErrorAlert error={report.error} />}
      {d && (
        <>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('الشحنات', 'Shipments')} value={formatNumber(d.totals.total)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard tone="good" label={tx('سُلّمت', 'Delivered')} value={formatNumber(d.totals.delivered)} hint={formatPercent(d.totals.deliveredPercent, language)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard tone="serious" label={tx('فشلت / مرتجعة', 'Failed / returned')} value={formatNumber(d.totals.failed)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('قيد التنفيذ', 'In progress')} value={formatNumber(d.totals.inProgress)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('الأجور', 'Charges')} value={formatMoney(d.totals.totalCharges)} /></Grid>
            <Grid size={{ xs: 6, md: 2 }}><StatCard label={tx('مبالغ التحصيل', 'COD amounts')} value={formatMoney(d.totals.codAmount)} /></Grid>
          </Grid>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid size={{ xs: 12, lg: 7 }}>
              <Card sx={{ height: '100%' }}>
                <CardHeader title={tx('يومياً', 'Daily')} titleTypographyProps={{ variant: 'subtitle1', fontWeight: 700 }} /><Divider />
                <CardContent><TrendChart data={d.daily.map((x) => ({ ...x, label: x.date.slice(5, 10) }))} xKey="label" series={[{ key: 'created', name: tx('أُنشئت', 'Created') }, { key: 'delivered', name: tx('سُلّمت', 'Delivered') }]} /></CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, lg: 5 }}>
              <Card sx={{ height: '100%' }}>
                <CardHeader title={tx('حسب الحالة الحالية', 'By current status')} titleTypographyProps={{ variant: 'subtitle1', fontWeight: 700 }} /><Divider />
                <CardContent><CategoryBarChart data={d.byStatus.map((s) => ({ label: enumLabel('ShipmentStatus', s.status, language), count: s.count }))} labelKey="label" valueKey="count" valueName={tx('الشحنات', 'Shipments')} /></CardContent>
              </Card>
            </Grid>
          </Grid>
          <Card>
            <Tabs value={tab} onChange={(_, v: number) => setTab(v)} variant="scrollable" sx={{ borderBottom: 1, borderColor: 'divider' }}>
              <Tab label={tx('حسب الفرع', 'By branch')} /><Tab label={tx('حسب السائق', 'By driver')} /><Tab label={tx('حسب العميل', 'By customer')} /><Tab label={tx('حسب نوع البضاعة', 'By goods type')} />
            </Tabs>
            <GroupTable rows={groups[tab] ?? []} />
          </Card>
        </>
      )}
    </>
  );
}
