import { Fragment, useState, type ReactNode } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Alert, Box, Button, Card, Checkbox, Chip, FormControlLabel, LinearProgress, MenuItem, Stack, Tab, Table, TableBody, TableCell, TableFooter, TableHead, TableRow, Tabs, TextField, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { BalanceSheetDto, CashFlowDto, CustomerAgingReportDto, GeneralLedgerDto, ProfitLossDto, StatementGroupDto, TrialBalanceDto } from '@/api/generated';
import PageHeader from '@/components/common/PageHeader';
import ErrorAlert from '@/components/common/ErrorAlert';
import ExportMenu from '@/components/common/ExportMenu';
import LookupSelect from '@/components/form/LookupSelect';
import { useTx } from '@/i18n';
import { businessToday, formatDate, formatMoney } from '@/utils/format';
import ReportFilters, { type ReportFilterValues } from './ReportFilters';

const firstOfMonth = () => `${businessToday().slice(0, 8)}01`;
const firstOfYear = () => `${businessToday().slice(0, 4)}-01-01`;
const m = (value: number | null | undefined) => formatMoney(value ?? 0);
const num = { fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap' } as const;

function useReport<T>(key: string, url: string, params: object, enabled = true) {
  return useQuery({ queryKey: [url, key, params], queryFn: () => api.get<T>(url, params), enabled, placeholderData: keepPreviousData });
}

function Loading({ busy, error, children }: { busy: boolean; error: unknown; children: ReactNode }) {
  return (
    <>
      {busy && <LinearProgress sx={{ mb: 1 }} />}
      {error ? <ErrorAlert error={error} /> : children}
    </>
  );
}

function TrialBalance() {
  const tx = useTx();
  const [values, setValues] = useState<ReportFilterValues>({ fromDate: firstOfYear(), toDate: businessToday(), branchId: null, includeZeroBalances: false, level: null });
  const report = useReport<TrialBalanceDto>('tb', '/accounting/trial-balance', values);
  const d = report.data;
  return (
    <>
      <ReportFilters values={values} onChange={setValues} lookups={[{ name: 'branchId', label: tx('الفرع', 'Branch'), source: 'branches' }]}>
        <TextField select label={tx('المستوى', 'Level')} value={values.level ?? ''} onChange={(e) => setValues({ ...values, level: e.target.value === '' ? null : Number(e.target.value) })} sx={{ width: 120 }}>
          <MenuItem value="">{tx('الكل', 'All')}</MenuItem>{[1, 2, 3, 4].map((l) => <MenuItem key={l} value={l}>{l}</MenuItem>)}
        </TextField>
        <FormControlLabel control={<Checkbox checked={!!values.includeZeroBalances} onChange={(e) => setValues({ ...values, includeZeroBalances: e.target.checked })} />} label={tx('الأرصدة الصفرية', 'Zero balances')} />
        <ExportMenu url="/accounting/trial-balance/export" params={values} />
      </ReportFilters>
      <Loading busy={report.isFetching} error={report.error}>
        {d && (
          <>
            <Alert severity={d.isBalanced ? 'success' : 'error'} sx={{ mb: 1 }}>{d.isBalanced ? tx('ميزان المراجعة متوازن', 'The trial balance is balanced') : tx('ميزان المراجعة غير متوازن!', 'The trial balance is NOT balanced!')} · {d.baseCurrencyCode}</Alert>
            <Card sx={{ overflowX: 'auto' }}>
              <Table size="small">
                <TableHead>
                  <TableRow><TableCell rowSpan={2}>{tx('الحساب', 'Account')}</TableCell><TableCell colSpan={2} align="center">{tx('الافتتاحي', 'Opening')}</TableCell><TableCell colSpan={2} align="center">{tx('حركة الفترة', 'Period')}</TableCell><TableCell colSpan={2} align="center">{tx('الختامي', 'Closing')}</TableCell></TableRow>
                  <TableRow>{[0, 1, 2].flatMap((i) => [<TableCell key={`d${i}`}>{tx('مدين', 'Debit')}</TableCell>, <TableCell key={`c${i}`}>{tx('دائن', 'Credit')}</TableCell>])}</TableRow>
                </TableHead>
                <TableBody>
                  {d.rows.map((r) => (
                    <TableRow key={r.accountId} sx={{ '& td': { fontWeight: r.isPostable ? 400 : 700 } }}>
                      <TableCell sx={{ paddingInlineStart: `${r.level * 12}px` }}><span dir="ltr">{r.code}</span> - {r.arabicName}</TableCell>
                      {[r.openingDebit, r.openingCredit, r.periodDebit, r.periodCredit, r.closingDebit, r.closingCredit].map((v, i) => <TableCell key={i} sx={num}>{v ? m(v) : ''}</TableCell>)}
                    </TableRow>
                  ))}
                </TableBody>
                <TableFooter>
                  <TableRow sx={{ '& td': { fontWeight: 700, color: 'text.primary' } }}>
                    <TableCell>{tx('الإجمالي', 'Total')}</TableCell>
                    {[d.totalOpeningDebit, d.totalOpeningCredit, d.totalPeriodDebit, d.totalPeriodCredit, d.totalClosingDebit, d.totalClosingCredit].map((v, i) => <TableCell key={i} sx={num}>{m(v)}</TableCell>)}
                  </TableRow>
                </TableFooter>
              </Table>
            </Card>
          </>
        )}
      </Loading>
    </>
  );
}

function GeneralLedger() {
  const tx = useTx();
  const [values, setValues] = useState<ReportFilterValues>({ fromDate: firstOfMonth(), toDate: businessToday(), accountId: null, branchId: null });
  const [page, setPage] = useState(1);
  const params = { ...values, pageNumber: page, pageSize: 100 };
  const report = useReport<GeneralLedgerDto>('gl', '/accounting/general-ledger', params, !!values.accountId);
  const d = report.data;
  return (
    <>
      <ReportFilters values={values} onChange={(v) => { setValues(v); setPage(1); }} lookups={[{ name: 'branchId', label: tx('الفرع', 'Branch'), source: 'branches' }]}>
        <Box sx={{ width: 320 }}><LookupSelect source={{ url: '/accounting/accounts', params: { isActive: true }, label: (r) => `${r.code} - ${r.arabicName}` }} label={tx('الحساب', 'Account')} required value={(values.accountId as number) ?? null} onChange={(v) => { setValues({ ...values, accountId: v as number }); setPage(1); }} /></Box>
        {!!values.accountId && <ExportMenu url="/accounting/general-ledger/export" params={values} />}
      </ReportFilters>
      {!values.accountId && <Alert severity="info">{tx('اختر حساباً لعرض دفتر الأستاذ.', 'Choose an account to show its ledger.')}</Alert>}
      <Loading busy={report.isFetching} error={report.error}>
        {d && (
          <Card sx={{ overflowX: 'auto' }}>
            <Stack direction="row" spacing={2} sx={{ p: 2, flexWrap: 'wrap' }} useFlexGap>
              <Chip label={`${tx('الافتتاحي', 'Opening')}: ${m(d.openingBalance)}`} />
              <Chip label={`${tx('مدين الفترة', 'Period debit')}: ${m(d.periodDebit)}`} />
              <Chip label={`${tx('دائن الفترة', 'Period credit')}: ${m(d.periodCredit)}`} />
              <Chip color="primary" label={`${tx('الختامي', 'Closing')}: ${m(d.closingBalance)}`} />
            </Stack>
            <Table size="small">
              <TableHead><TableRow><TableCell>{tx('التاريخ', 'Date')}</TableCell><TableCell>{tx('القيد', 'Entry')}</TableCell><TableCell>{tx('البيان', 'Description')}</TableCell><TableCell>{tx('المصدر', 'Source')}</TableCell><TableCell>{tx('مدين', 'Debit')}</TableCell><TableCell>{tx('دائن', 'Credit')}</TableCell><TableCell>{tx('الرصيد', 'Balance')}</TableCell></TableRow></TableHead>
              <TableBody>
                {d.lines.items.map((l, i) => (
                  <TableRow key={`${l.journalEntryId}-${i}`}>
                    <TableCell>{formatDate(l.entryDate)}</TableCell><TableCell>{l.entryNumber}</TableCell><TableCell>{l.lineDescription ?? l.description}</TableCell><TableCell>{l.sourceNumber}</TableCell>
                    <TableCell sx={num}>{l.debit ? m(l.debit) : ''}</TableCell><TableCell sx={num}>{l.credit ? m(l.credit) : ''}</TableCell><TableCell sx={num}>{m(l.runningBalance)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <Stack direction="row" spacing={1} sx={{ p: 1, justifyContent: 'flex-end', alignItems: 'center' }}>
              <Button disabled={!d.lines.hasPreviousPage} onClick={() => setPage((p) => p - 1)}>‹</Button>
              <Typography>{d.lines.pageNumber} / {Math.max(1, d.lines.totalPages)}</Typography>
              <Button disabled={!d.lines.hasNextPage} onClick={() => setPage((p) => p + 1)}>›</Button>
            </Stack>
          </Card>
        )}
      </Loading>
    </>
  );
}

function StatementGroups({ title, groups, total }: { title: string; groups: StatementGroupDto[]; total: number }) {
  const tx = useTx();
  return (
    <>
      <TableRow><TableCell colSpan={2} sx={{ bgcolor: 'action.hover', fontWeight: 700 }}>{title}</TableCell></TableRow>
      {groups.map((g) => (
        <Fragment key={`g${g.code}`}>
          <TableRow><TableCell sx={{ fontWeight: 600, paddingInlineStart: 3 }}>{g.arabicName}</TableCell><TableCell sx={num}>{m(g.amount)}</TableCell></TableRow>
          {g.accounts.map((a) => <TableRow key={a.accountId}><TableCell sx={{ paddingInlineStart: 6, color: 'text.secondary' }}><span dir="ltr">{a.code}</span> - {a.arabicName}</TableCell><TableCell sx={{ ...num, color: 'text.secondary' }}>{m(a.amount)}</TableCell></TableRow>)}
        </Fragment>
      ))}
      <TableRow><TableCell sx={{ fontWeight: 700 }}>{`${tx('إجمالي', 'Total')} ${title}`}</TableCell><TableCell sx={{ ...num, fontWeight: 700 }}>{m(total)}</TableCell></TableRow>
    </>
  );
}

function ProfitLoss() {
  const tx = useTx();
  const [values, setValues] = useState<ReportFilterValues>({ fromDate: firstOfMonth(), toDate: businessToday(), branchId: null });
  const report = useReport<ProfitLossDto>('pl', '/accounting/profit-loss', values);
  const d = report.data;
  return (
    <>
      <ReportFilters values={values} onChange={setValues} lookups={[{ name: 'branchId', label: tx('الفرع', 'Branch'), source: 'branches' }]}><ExportMenu url="/accounting/profit-loss/export" params={values} /></ReportFilters>
      <Loading busy={report.isFetching} error={report.error}>
        {d && (
          <Card sx={{ maxWidth: 820 }}>
            <Table size="small">
              <TableBody>
                <StatementGroups title={tx('الإيرادات', 'Revenue')} groups={d.revenue} total={d.totalRevenue} />
                <StatementGroups title={tx('تكلفة الإيرادات', 'Cost of revenue')} groups={d.costOfRevenue} total={d.totalCostOfRevenue} />
                <TableRow><TableCell sx={{ fontWeight: 700 }}>{tx('مجمل الربح', 'Gross profit')}</TableCell><TableCell sx={{ ...num, fontWeight: 700 }}>{m(d.grossProfit)}</TableCell></TableRow>
                <StatementGroups title={tx('المصروفات', 'Expenses')} groups={d.expenses} total={d.totalExpenses} />
                <TableRow><TableCell sx={{ fontWeight: 800, fontSize: 16 }}>{tx('صافي الربح', 'Net profit')} ({d.netProfitMarginPercent}%)</TableCell><TableCell sx={{ ...num, fontWeight: 800, fontSize: 16, color: d.netProfit >= 0 ? 'success.main' : 'error.main' }}>{m(d.netProfit)}</TableCell></TableRow>
              </TableBody>
            </Table>
          </Card>
        )}
      </Loading>
    </>
  );
}

function BalanceSheet() {
  const tx = useTx();
  const [values, setValues] = useState<ReportFilterValues>({ asOfDate: businessToday(), branchId: null });
  const report = useReport<BalanceSheetDto>('bs', '/accounting/balance-sheet', values);
  const d = report.data;
  return (
    <>
      <ReportFilters values={values} onChange={setValues} dates={['asOfDate']} lookups={[{ name: 'branchId', label: tx('الفرع', 'Branch'), source: 'branches' }]}><ExportMenu url="/accounting/balance-sheet/export" params={values} /></ReportFilters>
      <Loading busy={report.isFetching} error={report.error}>
        {d && (
          <>
            <Alert severity={d.isBalanced ? 'success' : 'error'} sx={{ mb: 1, maxWidth: 820 }}>{d.isBalanced ? tx('الأصول = الخصوم + حقوق الملكية', 'Assets = liabilities + equity') : tx('الميزانية غير متوازنة', 'Not balanced')}</Alert>
            <Card sx={{ maxWidth: 820 }}>
              <Table size="small">
                <TableBody>
                  <StatementGroups title={tx('الأصول', 'Assets')} groups={d.assets} total={d.totalAssets} />
                  <StatementGroups title={tx('الخصوم', 'Liabilities')} groups={d.liabilities} total={d.totalLiabilities} />
                  <StatementGroups title={tx('حقوق الملكية', 'Equity')} groups={d.equity} total={d.totalEquity - d.currentYearProfit - d.priorYearsProfit} />
                  <TableRow><TableCell>{tx('أرباح السنوات السابقة', 'Prior years profit')}</TableCell><TableCell sx={num}>{m(d.priorYearsProfit)}</TableCell></TableRow>
                  <TableRow><TableCell>{tx('ربح السنة الحالية', 'Current year profit')}</TableCell><TableCell sx={num}>{m(d.currentYearProfit)}</TableCell></TableRow>
                  <TableRow><TableCell sx={{ fontWeight: 800 }}>{tx('الخصوم وحقوق الملكية', 'Liabilities and equity')}</TableCell><TableCell sx={{ ...num, fontWeight: 800 }}>{m(d.totalLiabilitiesAndEquity)}</TableCell></TableRow>
                </TableBody>
              </Table>
            </Card>
          </>
        )}
      </Loading>
    </>
  );
}

function CashFlow() {
  const tx = useTx();
  const [values, setValues] = useState<ReportFilterValues>({ fromDate: firstOfMonth(), toDate: businessToday(), branchId: null });
  const report = useReport<CashFlowDto>('cf', '/accounting/cash-flow', values);
  const d = report.data;
  return (
    <>
      <ReportFilters values={values} onChange={setValues} lookups={[{ name: 'branchId', label: tx('الفرع', 'Branch'), source: 'branches' }]} />
      <Loading busy={report.isFetching} error={report.error}>
        {d && (
          <Card sx={{ maxWidth: 820 }}>
            <Table size="small">
              <TableBody>
                <TableRow><TableCell sx={{ fontWeight: 700 }}>{tx('النقدية أول الفترة', 'Opening cash')}</TableCell><TableCell sx={num}>{m(d.openingCash)}</TableCell></TableRow>
                {d.sections.map((s) => (
                  <TableRow key={s.activity}><TableCell>{s.activityName} <Typography component="span" variant="caption" color="text.secondary">({tx('داخل', 'in')} {m(s.totalInflow)} / {tx('خارج', 'out')} {m(s.totalOutflow)})</Typography></TableCell><TableCell sx={num}>{m(s.net)}</TableCell></TableRow>
                ))}
                <TableRow><TableCell sx={{ fontWeight: 700 }}>{tx('صافي التغير', 'Net change')}</TableCell><TableCell sx={{ ...num, fontWeight: 700 }}>{m(d.netChange)}</TableCell></TableRow>
                <TableRow><TableCell sx={{ fontWeight: 800 }}>{tx('النقدية آخر الفترة', 'Closing cash')}</TableCell><TableCell sx={{ ...num, fontWeight: 800 }}>{m(d.closingCash)}</TableCell></TableRow>
              </TableBody>
            </Table>
          </Card>
        )}
      </Loading>
    </>
  );
}

function CustomerAging() {
  const tx = useTx();
  const [values, setValues] = useState<ReportFilterValues>({ branchId: null, onlyWithBalance: true });
  const report = useReport<CustomerAgingReportDto>('aging', '/reports/customer-aging', { ...values, pageSize: 100, sortBy: 'totalDue', sortDirection: 'desc' });
  const d = report.data;
  const cols: [string, keyof CustomerAgingReportDto['totals']][] = [
    [tx('جاري', 'Current'), 'current'], ['1-30', 'days1To30'], ['31-60', 'days31To60'], ['61-90', 'days61To90'], ['90+', 'days90Plus'], [tx('الإجمالي', 'Total'), 'totalDue']
  ];
  return (
    <>
      <ReportFilters values={values} onChange={setValues} dates={[]} lookups={[{ name: 'branchId', label: tx('الفرع', 'Branch'), source: 'branches' }]} />
      <Loading busy={report.isFetching} error={report.error}>
        {d && (
          <Card sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead><TableRow><TableCell>{tx('العميل', 'Customer')}</TableCell>{cols.map(([label]) => <TableCell key={label}>{label}</TableCell>)}</TableRow></TableHead>
              <TableBody>
                {d.customers.items.map((r) => <TableRow key={r.customerId}><TableCell>{r.customerCode} - {r.customerName}</TableCell>{cols.map(([label, key]) => <TableCell key={label} sx={{ ...num, color: key === 'days90Plus' && (r[key] as number) > 0 ? 'error.main' : undefined }}>{m(r[key] as number)}</TableCell>)}</TableRow>)}
              </TableBody>
              <TableFooter><TableRow><TableCell>{tx('الإجمالي', 'Total')}</TableCell>{cols.map(([label, key]) => <TableCell key={label} sx={num}>{m(d.totals[key] as number)}</TableCell>)}</TableRow></TableFooter>
            </Table>
          </Card>
        )}
      </Loading>
    </>
  );
}

/** Financial reports from posted journal entries; Excel / PDF (Arabic font, RTL, company header) from the API. */
export default function FinancialReportsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const [tab, setTab] = useState(0);
  const tabs = [
    [tx('ميزان المراجعة', 'Trial balance'), <TrialBalance key="tb" />],
    [tx('دفتر الأستاذ', 'General ledger'), <GeneralLedger key="gl" />],
    [tx('قائمة الدخل', 'Profit and loss'), <ProfitLoss key="pl" />],
    [tx('الميزانية', 'Balance sheet'), <BalanceSheet key="bs" />],
    [tx('التدفقات النقدية', 'Cash flow'), <CashFlow key="cf" />],
    [tx('أعمار الذمم', 'Receivables aging'), <CustomerAging key="ag" />]
  ] as const;
  return (
    <>
      <PageHeader title={t('nav.financialReports')} />
      <Card sx={{ mb: 2 }}><Tabs value={tab} onChange={(_, v: number) => setTab(v)} variant="scrollable">{tabs.map(([label]) => <Tab key={label} label={label} />)}</Tabs></Card>
      {tabs[tab][1]}
    </>
  );
}
