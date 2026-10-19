import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, Card, Chip, CircularProgress, Grid2 as Grid, IconButton, Stack, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, TextField, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { useTranslation } from 'react-i18next';
import { api, saveFile } from '@/api/client';
import type { CustomerAddressDto, CustomerBalanceDto, CustomerDto, CustomerStatementDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import { ActiveChip } from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { useNotify } from '@/components/common/Notifier';
import FormDialog from '@/components/form/FormDialog';
import type { FieldDef } from '@/components/form/fields';
import { useLanguage, useTx } from '@/i18n';
import { enumLabel } from '@/i18n/enums';
import { businessDayEndUtc, businessDayStartUtc, businessDaysAgo, businessToday, formatDate, formatMoney } from '@/utils/format';

/** Customer card: data, addresses (CRUD) and the account statement (Receivables.View) with Excel export. */
export default function CustomerDetailsPage() {
  const id = Number(useParams().id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [tab, setTab] = useState(0);
  const [address, setAddress] = useState<{ row?: CustomerAddressDto } | null>(null);
  const [from, setFrom] = useState(businessDaysAgo(90));
  const [to, setTo] = useState(businessToday());

  const customer = useQuery({ queryKey: ['/customers', id], queryFn: () => api.get<CustomerDto>(`/customers/${id}`) });
  const addresses = useQuery({ queryKey: ['/customers', id, 'addresses'], queryFn: () => api.get<CustomerAddressDto[]>(`/customers/${id}/addresses`) });
  const canSeeBalances = hasPermission(P.Receivables.View);
  // Step 9: what the customer owes now, per currency (shipments add to it, payments reduce it).
  const balances = useQuery({
    queryKey: ['/customers', id, 'balances'],
    queryFn: () => api.get<CustomerBalanceDto[]>(`/customers/${id}/balances`),
    enabled: canSeeBalances
  });
  const addressFields: FieldDef[] = [
    { name: 'label', label: tx('التسمية', 'Label'), required: true, span: 4 },
    { name: 'cityId', label: tx('المدينة', 'City'), type: 'lookup', lookup: 'cities', required: true, span: 4 },
    { name: 'zoneId', label: tx('المنطقة', 'Zone'), type: 'lookup', lookup: 'zones', span: 4, lookupParams: (v) => (v.cityId ? { cityId: v.cityId } : {}) },
    { name: 'addressLine', label: tx('العنوان', 'Address'), required: true, span: 12 },
    { name: 'landmark', label: tx('علامة مميزة', 'Landmark'), span: 6 },
    { name: 'buildingNumber', label: tx('رقم البناء', 'Building no.'), span: 3 },
    { name: 'postalCode', label: tx('الرمز البريدي', 'Postal code'), span: 3 },
    { name: 'contactName', label: tx('اسم المستلم', 'Contact name'), span: 4 },
    { name: 'phone', label: tx('الهاتف', 'Phone'), span: 4 },
    { name: 'alternatePhone', label: tx('هاتف بديل', 'Alt. phone'), span: 4 },
    { name: 'latitude', label: tx('خط العرض', 'Latitude'), type: 'number', span: 3 },
    { name: 'longitude', label: tx('خط الطول', 'Longitude'), type: 'number', span: 3 },
    { name: 'isDefault', label: tx('العنوان الافتراضي', 'Default address'), type: 'checkbox', span: 6 }
  ];

  if (customer.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (!customer.data) return <ErrorAlert error={customer.error} />;
  const c = customer.data;
  const canManage = hasPermission(P.Customers.Manage);

  return (
    <>
      <PageHeader back="/customers" title={`${c.code} - ${c.arabicName}`} subtitle={c.englishName ?? undefined}
        actions={
          <>
            {hasPermission(P.Payments.Create) && <Button variant="contained" onClick={() => navigate(`/payments?new=1&customerId=${c.id}`)}>{tx('تسجيل دفعة', 'Record payment')}</Button>}
            <Button variant="outlined" onClick={() => navigate(`/shipments?senderCustomerId=${c.id}`)}>{t('nav.shipments')}</Button>
          </>
        } />
      {canSeeBalances && (
        <Section title={tx('الدين المستحق على العميل', 'Amount owed by the customer')}>
          {balances.isLoading ? <CircularProgress size={22} /> : (
            <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}>
              {(balances.data ?? []).filter((b) => b.balance !== 0).map((b) => (
                <Chip
                  key={b.currencyId}
                  color={b.balance > 0 ? 'error' : 'success'}
                  sx={{ fontSize: 18, height: 40, px: 1, fontWeight: 700 }}
                  label={`${b.balance > 0 ? '' : tx('له: ', 'Credit: ')}${formatMoney(Math.abs(b.balance), b.currencyCode, language)}`}
                  onClick={() => setTab(1)}
                />
              ))}
              {(balances.data ?? []).every((b) => b.balance === 0) && <Typography color="text.secondary">{tx('لا يوجد دين على هذا العميل.', 'Nothing owed.')}</Typography>}
            </Stack>
          )}
        </Section>
      )}
      <Section>
        <InfoGrid items={[
          { label: tx('النوع', 'Type'), value: enumLabel('CustomerType', c.customerType, language) },
          { label: tx('الدور', 'Role'), value: enumLabel('CustomerRole', c.roles, language) },
          { label: tx('الهاتف', 'Phone'), value: <span dir="ltr">{c.phone}{c.alternatePhone ? ` / ${c.alternatePhone}` : ''}</span> },
          { label: tx('البريد', 'E-mail'), value: c.email },
          { label: tx('الفرع', 'Branch'), value: c.defaultBranchName },
          { label: tx('خطة التسعير', 'Pricing plan'), value: c.pricingPlanName },
          { label: tx('حد الائتمان', 'Credit limit'), value: formatMoney(c.creditLimit) },
          { label: t('common.status'), value: <ActiveChip active={c.isActive} /> }
        ]} />
      </Section>
      <Card>
        <Tabs value={tab} onChange={(_, v: number) => setTab(v)} sx={{ borderBottom: 1, borderColor: 'divider', px: 1 }}>
          <Tab label={`${tx('العناوين', 'Addresses')} (${addresses.data?.length ?? 0})`} />
          {hasPermission(P.Receivables.View) && <Tab label={tx('كشف الحساب', 'Statement')} />}
        </Tabs>
        <Box sx={{ p: 2 }}>
          {tab === 0 && (
            <>
              {canManage && <Button startIcon={<AddIcon />} onClick={() => setAddress({})} sx={{ mb: 1 }}>{tx('عنوان جديد', 'New address')}</Button>}
              <Table size="small">
                <TableHead><TableRow><TableCell>{tx('التسمية', 'Label')}</TableCell><TableCell>{tx('المدينة / المنطقة', 'City / zone')}</TableCell><TableCell>{tx('العنوان', 'Address')}</TableCell><TableCell>{tx('الهاتف', 'Phone')}</TableCell><TableCell /></TableRow></TableHead>
                <TableBody>
                  {addresses.data?.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell>{a.label}{a.isDefault ? ` ★` : ''}</TableCell>
                      <TableCell>{[a.cityName, a.zoneName].filter(Boolean).join(' / ')}</TableCell>
                      <TableCell>{a.addressLine}{a.landmark ? ` (${a.landmark})` : ''}</TableCell>
                      <TableCell dir="ltr">{a.phone ?? '-'}</TableCell>
                      <TableCell align="left">
                        {canManage && <IconButton size="small" onClick={() => setAddress({ row: a })}><EditOutlinedIcon fontSize="small" /></IconButton>}
                        {canManage && <IconButton size="small" color="error" onClick={async () => {
                          try { await api.delete(`/customers/${id}/addresses/${a.id}`); await queryClient.invalidateQueries({ queryKey: ['/customers', id] }); } catch (e) { notify.error(e); }
                        }}><DeleteOutlineIcon fontSize="small" /></IconButton>}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </>
          )}
          {tab === 1 && (
            <>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mb: 2 }}>
                <TextField type="date" label={t('common.fromDate')} value={from} onChange={(e) => setFrom(e.target.value)} InputLabelProps={{ shrink: true }} sx={{ maxWidth: 200 }} />
                <TextField type="date" label={t('common.toDate')} value={to} onChange={(e) => setTo(e.target.value)} InputLabelProps={{ shrink: true }} sx={{ maxWidth: 200 }} />
              </Stack>
              {balances.error && <ErrorAlert error={balances.error} />}
              {/* Step 9: one statement per currency (dollar and dinar debts are never added together). */}
              {(balances.data ?? []).length === 0 && !balances.isLoading && <Typography color="text.secondary">{t('common.noData')}</Typography>}
              {(balances.data ?? []).map((b) => (
                <StatementBlock key={b.currencyId} customerId={id} currencyId={b.currencyId} currencyCode={b.currencyCode} from={from} to={to} />
              ))}
            </>
          )}
        </Box>
      </Card>

      <FormDialog<Record<string, unknown>>
        open={!!address}
        title={address?.row ? tx('تعديل العنوان', 'Edit address') : tx('عنوان جديد', 'New address')}
        fields={addressFields}
        defaultValues={address?.row ? { ...address.row } : { label: tx('المنزل', 'Home'), isDefault: (addresses.data?.length ?? 0) === 0 }}
        onClose={() => setAddress(null)}
        onSubmit={async (values) => {
          if (address?.row) await api.put(`/customers/${id}/addresses/${address.row.id}`, values);
          else await api.post(`/customers/${id}/addresses`, values);
          notify.success(t('common.saved'));
          setAddress(null);
          await queryClient.invalidateQueries({ queryKey: ['/customers', id] });
        }}
      />
    </>
  );
}

/** Account statement of one currency: totals, movements with running balance, paging and Excel export. */
function StatementBlock({ customerId, currencyId, currencyCode, from, to }: { customerId: number; currencyId: number; currencyCode: string; from: string; to: string }) {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const notify = useNotify();
  const [page, setPage] = useState(1);
  const params = { fromUtc: businessDayStartUtc(from), toUtc: businessDayEndUtc(to), pageNumber: page, pageSize: 50, currencyId };
  const statement = useQuery({
    queryKey: ['/customers', customerId, 'statement', params],
    queryFn: () => api.get<CustomerStatementDto>(`/customers/${customerId}/statement`, params)
  });

  return (
    <Box sx={{ mb: 4 }}>
      <Stack direction="row" spacing={1} sx={{ mb: 1, alignItems: 'center' }}>
        <Typography variant="h6" sx={{ flex: 1 }}>{tx('كشف الحساب', 'Statement')} · <span dir="ltr">{currencyCode}</span></Typography>
        <Button variant="outlined" size="small" onClick={async () => {
          try { saveFile(await api.download(`/customers/${customerId}/statement/export`, { params, fallbackName: `statement-${currencyCode}.xlsx` })); } catch (e) { notify.error(e); }
        }}>{t('common.exportExcel')}</Button>
      </Stack>
      {statement.error && <ErrorAlert error={statement.error} />}
      {statement.isLoading && <CircularProgress size={22} />}
      {statement.data && (
        <>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            {[
              [tx('الرصيد الافتتاحي', 'Opening'), statement.data.openingBalance],
              [tx('مدين', 'Debit'), statement.data.totalDebit],
              [tx('دائن', 'Credit'), statement.data.totalCredit],
              [tx('الرصيد الختامي', 'Closing'), statement.data.closingBalance],
              [tx('متأخر', 'Overdue'), statement.data.overdueBalance]
            ].map(([label, value]) => (
              <Grid key={label as string} size={{ xs: 6, md: 2.4 }}><InfoGrid items={[{ label, value: formatMoney(value as number, currencyCode, language), span: 12 }]} /></Grid>
            ))}
          </Grid>
          <Table size="small">
            <TableHead><TableRow><TableCell>{t('common.date')}</TableCell><TableCell>{tx('المستند', 'Document')}</TableCell><TableCell>{tx('البيان', 'Description')}</TableCell><TableCell>{tx('مدين', 'Debit')}</TableCell><TableCell>{tx('دائن', 'Credit')}</TableCell><TableCell>{tx('الرصيد', 'Balance')}</TableCell></TableRow></TableHead>
            <TableBody>
              {statement.data.lines.items.map((line) => (
                <TableRow key={line.transactionId}>
                  <TableCell>{formatDate(line.transactionDateUtc)}</TableCell>
                  <TableCell>{line.transactionTypeName} {line.documentNumber}</TableCell>
                  <TableCell>{line.description}</TableCell>
                  <TableCell>{line.debit ? formatMoney(line.debit) : ''}</TableCell>
                  <TableCell>{line.credit ? formatMoney(line.credit) : ''}</TableCell>
                  <TableCell>{formatMoney(line.balance)}</TableCell>
                </TableRow>
              ))}
              {statement.data.lines.items.length === 0 && <TableRow><TableCell colSpan={6}>{tx('لا توجد حركات في هذه الفترة.', 'No movements in this period.')}</TableCell></TableRow>}
            </TableBody>
          </Table>
          {statement.data.lines.totalPages > 1 && (
            <Stack direction="row" spacing={1} sx={{ mt: 1, justifyContent: 'flex-end' }}>
              <Button disabled={!statement.data.lines.hasPreviousPage} onClick={() => setPage((p) => p - 1)}>‹</Button>
              <Box sx={{ alignSelf: 'center' }}>{statement.data.lines.pageNumber} / {statement.data.lines.totalPages}</Box>
              <Button disabled={!statement.data.lines.hasNextPage} onClick={() => setPage((p) => p + 1)}>›</Button>
            </Stack>
          )}
        </>
      )}
    </Box>
  );
}
