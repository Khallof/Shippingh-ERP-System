import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  Box, Card, CardContent, CardHeader, Divider, Grid2 as Grid, List, ListItem, ListItemText, MenuItem, Table, TableBody,
  TableCell, TableHead, TableRow, TextField, Typography
} from '@mui/material';
import TodayOutlinedIcon from '@mui/icons-material/TodayOutlined';
import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import LocalShippingOutlinedIcon from '@mui/icons-material/LocalShippingOutlined';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import SavingsOutlinedIcon from '@mui/icons-material/SavingsOutlined';
import BadgeOutlinedIcon from '@mui/icons-material/BadgeOutlined';
import HandshakeOutlinedIcon from '@mui/icons-material/HandshakeOutlined';
import TrendingUpIcon from '@mui/icons-material/TrendingUp';
import TrendingDownIcon from '@mui/icons-material/TrendingDown';
import AccountBalanceWalletOutlinedIcon from '@mui/icons-material/AccountBalanceWalletOutlined';
import ReceiptLongOutlinedIcon from '@mui/icons-material/ReceiptLongOutlined';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { DashboardDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import PageHeader from '@/components/common/PageHeader';
import StatCard from '@/components/common/StatCard';
import StatusChip from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import LookupSelect from '@/components/form/LookupSelect';
import { CategoryBarChart, TrendChart } from '@/components/charts/Charts';
import { enumLabel } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { formatDateTime, formatMoney, formatNumber, formatPercent } from '@/utils/format';

function Panel({ title, children, action }: { title: string; children: React.ReactNode; action?: React.ReactNode }) {
  return (
    <Card sx={{ height: '100%' }}>
      <CardHeader title={title} action={action} titleTypographyProps={{ variant: 'subtitle1', fontWeight: 700 }} sx={{ py: 1.25 }} />
      <Divider />
      <CardContent>{children}</CardContent>
    </Card>
  );
}

/**
 * Operational + financial dashboard (GET /dashboard). The API returns only the sections the user may see
 * (operations: Shipments.View, COD: COD.View, cashboxes: Cashboxes.View, finance: Reports.ViewFinancial);
 * a hidden section is null and its cards are not drawn.
 */
export default function DashboardPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const navigate = useNavigate();
  const { user } = useAuth();
  const [days, setDays] = useState(30);
  const [branchId, setBranchId] = useState<number | null>(null);

  const query = useQuery({
    queryKey: ['/dashboard', days, branchId],
    queryFn: () => api.get<DashboardDto>('/dashboard', { days, branchId }),
    refetchInterval: 120_000
  });

  const data = query.data;
  const ops = data?.operations;
  const cod = data?.cod;
  const finance = data?.finance;
  const loading = query.isLoading;
  const money = (value: number | undefined | null, currency?: string | null) => formatMoney(value ?? 0, currency, language);

  const codStage = (status: string) => cod?.stages.find((stage) => stage.status === status);
  const withDrivers = codStage('CollectedByDriver');
  const ready = codStage('ReadyForSenderSettlement');

  return (
    <>
      <PageHeader
        title={t('dashboard.title')}
        subtitle={`${language === 'ar' ? user?.arabicFullName : user?.fullName} · ${data ? formatDateTime(data.asOfUtc) : ''}`}
        actions={
          <>
            <Box sx={{ width: 220 }}>
              <LookupSelect source="branches" label={t('common.branch')} value={branchId} onChange={(value) => setBranchId(value as number | null)} />
            </Box>
            <TextField select label={t('dashboard.period')} value={days} onChange={(event) => setDays(Number(event.target.value))} sx={{ width: 140 }}>
              {[7, 30, 90, 365].map((value) => <MenuItem key={value} value={value}>{value}</MenuItem>)}
            </TextField>
          </>
        }
      />

      {query.error && <ErrorAlert error={query.error} onRetry={() => void query.refetch()} />}

      {(ops || loading) && (
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid size={{ xs: 6, md: 4, lg: 2 }}><StatCard loading={loading} icon={<TodayOutlinedIcon />} label={t('dashboard.todayShipments')} value={formatNumber(ops?.todayCreated)} onClick={() => navigate('/shipments')} /></Grid>
          <Grid size={{ xs: 6, md: 4, lg: 2 }}><StatCard loading={loading} icon={<CheckCircleOutlineIcon />} tone="good" label={t('dashboard.delivered')} value={formatNumber(ops?.todayDelivered)} /></Grid>
          <Grid size={{ xs: 6, md: 4, lg: 2 }}><StatCard loading={loading} icon={<ErrorOutlineIcon />} tone="serious" label={t('dashboard.failedToday')} value={formatNumber(ops?.todayFailed)} /></Grid>
          <Grid size={{ xs: 6, md: 4, lg: 2 }}><StatCard loading={loading} icon={<LocalShippingOutlinedIcon />} label={t('dashboard.outForDelivery')} value={formatNumber(ops?.outForDelivery)} onClick={() => navigate('/shipments?status=OutForDelivery')} /></Grid>
          <Grid size={{ xs: 6, md: 4, lg: 2 }}><StatCard loading={loading} icon={<CheckCircleOutlineIcon />} tone="good" label={`${t('dashboard.deliveredPercent')} (${days})`} value={formatPercent(ops?.deliveredPercent, language)} hint={`${formatNumber(ops?.periodDelivered)} / ${formatNumber(ops?.periodShipments)}`} /></Grid>
          <Grid size={{ xs: 6, md: 4, lg: 2 }}><StatCard loading={loading} icon={<ErrorOutlineIcon />} tone="critical" label={`${t('dashboard.failedPercent')} (${days})`} value={formatPercent(ops?.failedPercent, language)} hint={`${formatNumber(ops?.periodFailed)} / ${formatNumber(ops?.periodShipments)}`} /></Grid>
        </Grid>
      )}

      {cod && (
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<HourglassEmptyIcon />} tone="warning" label={t('dashboard.codPending')} value={money(cod.deliveredNotCollectedAmount, cod.currencyCode)} hint={`${formatNumber(cod.deliveredNotCollectedCount)} ${tx('شحنة مُسلّمة', 'delivered')}`} onClick={() => navigate('/cod')} /></Grid>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<BadgeOutlinedIcon />} label={t('dashboard.codWithDrivers')} value={money(cod.driversOutstandingTotal, cod.currencyCode)} hint={`${formatNumber(cod.driversWithBalance)} ${tx('سائق', 'drivers')} · ${formatNumber(withDrivers?.count)} ${tx('شحنة', 'shipments')}`} onClick={() => navigate('/cod/driver-balances')} /></Grid>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<HandshakeOutlinedIcon />} tone="good" label={t('dashboard.codReady')} value={money(ready?.amount, cod.currencyCode)} hint={`${formatNumber(ready?.count)} ${tx('شحنة', 'shipments')} · ${tx('صافي غير مسوّى', 'unsettled net')} ${money(cod.unsettledSenderNet)}`} onClick={() => navigate('/cod/settlements')} /></Grid>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<SavingsOutlinedIcon />} label={t('dashboard.cashboxes')} value={money(cod.cashboxBalanceTotal, cod.currencyCode)} hint={`${formatNumber(cod.cashboxCount)} ${tx('صندوق', 'cashboxes')}`} onClick={() => navigate('/cash/cashboxes')} /></Grid>
        </Grid>
      )}

      {finance && (
        <Grid container spacing={2} sx={{ mb: 2 }}>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<TrendingUpIcon />} tone="good" label={t('dashboard.revenue')} value={money(finance.totalRevenue, finance.baseCurrencyCode)} hint={`${finance.fromDate} → ${finance.toDate}`} /></Grid>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<TrendingDownIcon />} tone="serious" label={t('dashboard.expenses')} value={money(finance.totalExpenses, finance.baseCurrencyCode)} /></Grid>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<AccountBalanceWalletOutlinedIcon />} tone={finance.netProfit >= 0 ? 'good' : 'critical'} label={t('dashboard.netProfit')} value={money(finance.netProfit, finance.baseCurrencyCode)} hint={tx('من القيود المرحّلة', 'From posted entries')} /></Grid>
          <Grid size={{ xs: 12, sm: 6, lg: 3 }}><StatCard icon={<ReceiptLongOutlinedIcon />} tone={finance.overdueInvoicesCount > 0 ? 'critical' : 'good'} label={t('dashboard.overdueInvoices')} value={money(finance.overdueInvoicesAmount, finance.baseCurrencyCode)} hint={`${formatNumber(finance.overdueInvoicesCount)} ${tx('فاتورة', 'invoices')} · ${t('dashboard.receivables')} ${money(finance.receivablesBalance)}`} onClick={() => navigate('/invoices?status=Overdue')} /></Grid>
        </Grid>
      )}

      <Grid container spacing={2}>
        {ops && (
          <>
            <Grid size={{ xs: 12, lg: 7 }}>
              <Panel title={t('dashboard.last7Days')}>
                <TrendChart
                  data={ops.last7Days.map((day) => ({ ...day, label: day.date.slice(5, 10) }))}
                  xKey="label"
                  series={[{ key: 'created', name: t('dashboard.created') }, { key: 'delivered', name: tx('سُلّمت', 'Delivered') }]}
                />
              </Panel>
            </Grid>
            <Grid size={{ xs: 12, lg: 5 }}>
              <Panel title={`${t('dashboard.byStatus')} · ${tx('الشحنات المفتوحة', 'Open shipments')} ${formatNumber(ops.openShipments)}`}>
                <CategoryBarChart
                  data={ops.byStatus.map((row) => ({ ...row, label: enumLabel('ShipmentStatus', row.status, language) }))}
                  labelKey="label"
                  valueKey="count"
                  valueName={t('dashboard.shipmentsCount')}
                />
              </Panel>
            </Grid>
          </>
        )}

        {data?.cashboxes && data.cashboxes.length > 0 && (
          <Grid size={{ xs: 12, md: 6 }}>
            <Panel title={t('dashboard.cashboxes')}>
              <Table size="small">
                <TableHead><TableRow><TableCell>{tx('الصندوق', 'Cashbox')}</TableCell><TableCell>{t('common.branch')}</TableCell><TableCell align="left">{tx('الرصيد', 'Balance')}</TableCell></TableRow></TableHead>
                <TableBody>
                  {data.cashboxes.map((box) => (
                    <TableRow key={box.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/cash/cashboxes/${box.id}`)}>
                      <TableCell>{box.code} - {box.name}</TableCell>
                      <TableCell>{box.branchName}</TableCell>
                      <TableCell align="left" sx={{ fontVariantNumeric: 'tabular-nums' }}>{money(box.balance, box.currencyCode)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Panel>
          </Grid>
        )}

        {data?.driverBalances && data.driverBalances.length > 0 && (
          <Grid size={{ xs: 12, md: 6 }}>
            <Panel title={t('dashboard.driverBalances')}>
              <Table size="small">
                <TableHead><TableRow><TableCell>{t('common.driver')}</TableCell><TableCell>{tx('شحنات لم تُسلّم نقديتها', 'Unhanded COD')}</TableCell><TableCell align="left">{tx('العهدة', 'Balance')}</TableCell></TableRow></TableHead>
                <TableBody>
                  {data.driverBalances.map((driver) => (
                    <TableRow key={driver.driverId}>
                      <TableCell>{driver.driverName}<Typography variant="caption" color="text.secondary" display="block">{driver.branchName}</Typography></TableCell>
                      <TableCell>{formatNumber(driver.unhandedCodCount)}</TableCell>
                      <TableCell align="left" sx={{ fontVariantNumeric: 'tabular-nums' }}>{money(driver.balance, driver.currencyCode)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Panel>
          </Grid>
        )}

        {data?.topCustomers && data.topCustomers.length > 0 && (
          <Grid size={{ xs: 12, md: 6 }}>
            <Panel title={`${t('dashboard.topCustomers')} (${days})`}>
              <Table size="small">
                <TableHead><TableRow><TableCell>{t('common.customer')}</TableCell><TableCell>{t('dashboard.shipmentsCount')}</TableCell><TableCell align="left">{t('dashboard.charges')}</TableCell></TableRow></TableHead>
                <TableBody>
                  {data.topCustomers.map((customer) => (
                    <TableRow key={customer.customerId} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/customers/${customer.customerId}`)}>
                      <TableCell>{customer.code} - {customer.name}</TableCell>
                      <TableCell>{formatNumber(customer.shipmentCount)}</TableCell>
                      <TableCell align="left" sx={{ fontVariantNumeric: 'tabular-nums' }}>{money(customer.totalCharges)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Panel>
          </Grid>
        )}

        {data?.recentActivity && data.recentActivity.length > 0 && (
          <Grid size={{ xs: 12, md: 6 }}>
            <Panel title={t('dashboard.recentActivity')}>
              <List dense disablePadding>
                {data.recentActivity.map((activity, index) => (
                  <ListItem key={index} disableGutters divider secondaryAction={<StatusChip enumName="ShipmentStatus" value={activity.status} />} sx={{ cursor: 'pointer', pr: 18 }} onClick={() => navigate(`/shipments/${activity.shipmentId}`)}>
                    <ListItemText primary={`${activity.trackingNumber} · ${activity.branchName ?? ''}`} secondary={`${formatDateTime(activity.occurredAtUtc)}${activity.notes ? ` · ${activity.notes}` : ''}`} />
                  </ListItem>
                ))}
              </List>
            </Panel>
          </Grid>
        )}
      </Grid>
    </>
  );
}
