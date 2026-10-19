import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Controller, useFieldArray, useForm } from 'react-hook-form';
import {
  Alert, Box, Button, CircularProgress, Grid2 as Grid, IconButton, MenuItem, Stack, Table, TableBody, TableCell, TableFooter, TableHead,
  TableRow, TextField, Typography
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { useTranslation } from 'react-i18next';
import { api, idempotent, newIdempotencyKey } from '@/api/client';
import { AppError, toAppError } from '@/api/errors';
import type { JournalEntryDto, JournalType, SaveJournalEntryRequest } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import Section from '@/components/common/Section';
import InfoGrid from '@/components/common/InfoGrid';
import StatusChip from '@/components/common/StatusChip';
import ErrorAlert from '@/components/common/ErrorAlert';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import LookupSelect from '@/components/form/LookupSelect';
import { enumLabel } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { businessToday, formatDate, formatDateTime, formatMoney } from '@/utils/format';

const MANUAL_TYPES: JournalType[] = ['General', 'Opening', 'Adjustment', 'Bank'];

interface LineValues {
  accountId: number | null;
  description: string | null;
  debit: number;
  credit: number;
  branchId: number | null;
}

interface Values {
  entryDate: string;
  journalType: JournalType;
  branchId: number | null;
  description: string;
  reference: string | null;
  lines: LineValues[];
}

const emptyLine: LineValues = { accountId: null, description: null, debit: 0, credit: 0, branchId: null };
const round2 = (value: number) => Math.round(value * 100) / 100;

/** Manual journal editor: balanced lines (debit = credit), live totals, save as draft or save and submit. */
function JournalEditor({ entry }: { entry?: JournalEntryDto }) {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [error, setError] = useState<AppError | null>(null);
  const key = useMemo(() => newIdempotencyKey(), []);

  const form = useForm<Values>({
    defaultValues: entry
      ? {
          entryDate: entry.entryDate.slice(0, 10), journalType: entry.journalType, branchId: entry.branchId, description: entry.description, reference: entry.reference,
          lines: entry.lines.map((l) => ({ accountId: l.accountId, description: l.description, debit: l.debit, credit: l.credit, branchId: l.branchId }))
        }
      : { entryDate: businessToday(), journalType: 'General', branchId: null, description: '', reference: null, lines: [{ ...emptyLine }, { ...emptyLine }] }
  });
  const lines = useFieldArray({ control: form.control, name: 'lines' });
  const watched = form.watch('lines');
  const totalDebit = round2(watched.reduce((sum, l) => sum + (Number(l.debit) || 0), 0));
  const totalCredit = round2(watched.reduce((sum, l) => sum + (Number(l.credit) || 0), 0));
  const balanced = totalDebit > 0 && totalDebit === totalCredit;

  const save = (submit: boolean) => form.handleSubmit(async (values) => {
    setError(null);
    const body: SaveJournalEntryRequest = {
      ...values,
      currencyId: null,
      exchangeRate: null,
      submit,
      lines: values.lines.map((l) => ({ ...l, accountId: l.accountId ?? 0, debit: Number(l.debit) || 0, credit: Number(l.credit) || 0, dimensions: [] }))
    };
    try {
      const saved = entry
        ? await api.put<JournalEntryDto>(`/accounting/journal-entries/${entry.id}`, body)
        : await api.post<JournalEntryDto>('/accounting/journal-entries', body, idempotent(key));
      notify.success(t('common.saved'));
      await queryClient.invalidateQueries({ queryKey: ['/accounting/journal-entries'] });
      navigate(`/accounting/journal-entries/${saved.id}`, { replace: true });
    } catch (caught) {
      setError(await toAppError(caught));
    }
  });

  return (
    <>
      <PageHeader back="/accounting/journal-entries" title={entry ? `${t('common.edit')} ${entry.entryNumber ?? tx('مسودة', 'draft')}` : tx('قيد يومية يدوي', 'Manual journal entry')}
        actions={
          <>
            <Button variant="outlined" onClick={save(false)} disabled={form.formState.isSubmitting}>{tx('حفظ كمسودة', 'Save draft')}</Button>
            <Button variant="contained" onClick={save(true)} disabled={form.formState.isSubmitting || !balanced}>{tx('حفظ وتقديم للاعتماد', 'Save and submit')}</Button>
          </>
        } />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error.message}<ul style={{ margin: 0 }}>{Object.entries(error.fieldErrors).map(([k, v]) => <li key={k}>{v.join(' · ')}</li>)}</ul></Alert>}
      <Section>
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 3 }}><TextField type="date" label={t('common.date')} InputLabelProps={{ shrink: true }} {...form.register('entryDate', { required: true })} /></Grid>
          <Grid size={{ xs: 12, sm: 3 }}>
            <Controller control={form.control} name="journalType" render={({ field }) => (
              <TextField select label={tx('اليومية', 'Journal')} value={field.value} onChange={field.onChange}>
                {MANUAL_TYPES.map((type) => <MenuItem key={type} value={type}>{enumLabel('JournalType', type, language)}</MenuItem>)}
              </TextField>
            )} />
          </Grid>
          <Grid size={{ xs: 12, sm: 3 }}><Controller control={form.control} name="branchId" render={({ field }) => <LookupSelect source="branches" label={t('common.branch')} value={field.value} onChange={(v) => field.onChange(v)} />} /></Grid>
          <Grid size={{ xs: 12, sm: 3 }}><TextField label={tx('المرجع', 'Reference')} {...form.register('reference', { setValueAs: (v) => v || null })} /></Grid>
          <Grid size={12}><TextField label={tx('البيان', 'Description')} required {...form.register('description', { required: true })} error={!!form.formState.errors.description} /></Grid>
        </Grid>
      </Section>
      <Section title={tx('الأسطر', 'Lines')} action={<Button startIcon={<AddIcon />} onClick={() => lines.append({ ...emptyLine })}>{tx('سطر', 'Line')}</Button>} noPadding>
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead><TableRow><TableCell sx={{ minWidth: 280 }}>{tx('الحساب', 'Account')}</TableCell><TableCell sx={{ minWidth: 200 }}>{tx('البيان', 'Description')}</TableCell><TableCell sx={{ width: 150 }}>{tx('مدين', 'Debit')}</TableCell><TableCell sx={{ width: 150 }}>{tx('دائن', 'Credit')}</TableCell><TableCell sx={{ minWidth: 180 }}>{t('common.branch')}</TableCell><TableCell /></TableRow></TableHead>
            <TableBody>
              {lines.fields.map((item, index) => (
                <TableRow key={item.id}>
                  <TableCell><Controller control={form.control} name={`lines.${index}.accountId`} render={({ field }) => <LookupSelect source="accounts" label={tx('الحساب', 'Account')} value={field.value} onChange={(v) => field.onChange(v)} />} /></TableCell>
                  <TableCell><TextField {...form.register(`lines.${index}.description`)} /></TableCell>
                  <TableCell><TextField type="number" inputProps={{ step: 0.01, min: 0, dir: 'ltr' }} {...form.register(`lines.${index}.debit`, { valueAsNumber: true, onChange: (e) => Number(e.target.value) > 0 && form.setValue(`lines.${index}.credit`, 0) })} /></TableCell>
                  <TableCell><TextField type="number" inputProps={{ step: 0.01, min: 0, dir: 'ltr' }} {...form.register(`lines.${index}.credit`, { valueAsNumber: true, onChange: (e) => Number(e.target.value) > 0 && form.setValue(`lines.${index}.debit`, 0) })} /></TableCell>
                  <TableCell><Controller control={form.control} name={`lines.${index}.branchId`} render={({ field }) => <LookupSelect source="branches" label={t('common.branch')} value={field.value} onChange={(v) => field.onChange(v)} />} /></TableCell>
                  <TableCell><IconButton color="error" disabled={lines.fields.length <= 2} onClick={() => lines.remove(index)}><DeleteOutlineIcon /></IconButton></TableCell>
                </TableRow>
              ))}
            </TableBody>
            <TableFooter>
              <TableRow>
                <TableCell colSpan={2}><Typography fontWeight={700}>{t('common.total')}</Typography></TableCell>
                <TableCell><Typography fontWeight={700}>{formatMoney(totalDebit)}</Typography></TableCell>
                <TableCell><Typography fontWeight={700}>{formatMoney(totalCredit)}</Typography></TableCell>
                <TableCell colSpan={2}>
                  {balanced ? <Typography color="success.main">{tx('متوازن ✓', 'Balanced ✓')}</Typography> : <Typography color="error.main">{tx('الفرق', 'Difference')}: {formatMoney(round2(totalDebit - totalCredit))}</Typography>}
                </TableCell>
              </TableRow>
            </TableFooter>
          </Table>
        </Box>
      </Section>
    </>
  );
}

/** Journal entry: view with workflow actions, or the editor for new / draft / rejected manual entries. */
export default function JournalEntryPage() {
  const { id } = useParams();
  const isNew = id === 'new';
  const entryId = isNew ? null : Number(id);
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [dialog, setDialog] = useState<'reject' | 'reverse' | null>(null);
  const [editing, setEditing] = useState(false);
  const entry = useQuery({ queryKey: ['/accounting/journal-entries', entryId], queryFn: () => api.get<JournalEntryDto>(`/accounting/journal-entries/${entryId}`), enabled: !isNew });
  useEffect(() => setEditing(false), [entryId]);

  const run = async (action: () => Promise<unknown>) => {
    try {
      await action();
      notify.success(t('common.done'));
      setDialog(null);
      await queryClient.invalidateQueries({ queryKey: ['/accounting/journal-entries'] });
    } catch (error) {
      notify.error(error);
    }
  };

  if (isNew) return <JournalEditor />;
  if (entry.isLoading) return <Box sx={{ py: 8, textAlign: 'center' }}><CircularProgress /></Box>;
  if (!entry.data) return <ErrorAlert error={entry.error} />;
  const e = entry.data;
  if (editing) return <JournalEditor entry={e} />;
  const url = `/accounting/journal-entries/${e.id}`;
  const editable = !e.isSystemGenerated && ['Draft', 'Rejected'].includes(e.status) && hasPermission(P.Accounting.CreateJournal);

  return (
    <>
      <PageHeader back="/accounting/journal-entries" title={<Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}><span>{e.entryNumber ?? tx('مسودة قيد', 'Draft entry')}</span><StatusChip enumName="JournalEntryStatus" value={e.status} /></Stack>}
        subtitle={`${enumLabel('JournalType', e.journalType, language)} · ${formatDate(e.entryDate)} · ${e.periodName}`}
        actions={
          <>
            {editable && <Button variant="outlined" onClick={() => setEditing(true)}>{t('common.edit')}</Button>}
            {editable && e.status === 'Draft' && <Button variant="contained" onClick={() => void run(() => api.post(`${url}/submit`))}>{t('common.submit')}</Button>}
            {editable && e.status === 'Draft' && <Button color="error" onClick={() => void run(async () => { await api.delete(url); navigate('/accounting/journal-entries'); })}>{t('common.delete')}</Button>}
            {e.status === 'Submitted' && hasPermission(P.Accounting.ApproveJournal) && <Button variant="contained" onClick={() => void run(() => api.post(`${url}/approve`, { notes: null }))}>{t('common.approve')}</Button>}
            {e.status === 'Submitted' && hasPermission(P.Accounting.ApproveJournal) && <Button color="error" onClick={() => setDialog('reject')}>{t('common.reject')}</Button>}
            {e.status === 'Approved' && hasPermission(P.Accounting.PostJournal) && <Button variant="contained" color="success" onClick={() => void run(() => api.post(`${url}/post`))}>{t('common.post')}</Button>}
            {e.status === 'Posted' && !e.reversedByEntryId && hasPermission(P.Accounting.ReverseJournal) && <Button color="error" onClick={() => setDialog('reverse')}>{t('common.reverse')}</Button>}
          </>
        } />
      {e.rejectionReason && <Alert severity="warning" sx={{ mb: 2 }}>{tx('سبب الرفض', 'Rejection reason')}: {e.rejectionReason}</Alert>}
      <Section>
        <InfoGrid items={[
          { label: tx('البيان', 'Description'), value: e.description, span: 6 },
          { label: tx('المرجع', 'Reference'), value: e.reference },
          { label: t('common.branch'), value: e.branchName },
          { label: tx('المصدر', 'Source'), value: e.isSystemGenerated ? `${e.sourceType ?? ''} ${e.sourceNumber ?? ''}` : tx('يدوي', 'Manual') },
          { label: tx('عكس القيد', 'Reverses'), value: e.reversalOfEntryNumber },
          { label: tx('معكوس بالقيد', 'Reversed by'), value: e.reversedByEntryNumber },
          { label: tx('رُحّل', 'Posted at'), value: formatDateTime(e.postedAtUtc) }
        ]} />
      </Section>
      <Section title={tx('الأسطر', 'Lines')} noPadding>
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead><TableRow><TableCell>#</TableCell><TableCell>{tx('الحساب', 'Account')}</TableCell><TableCell>{tx('البيان', 'Description')}</TableCell><TableCell>{t('common.branch')}</TableCell><TableCell>{tx('مدين', 'Debit')}</TableCell><TableCell>{tx('دائن', 'Credit')}</TableCell></TableRow></TableHead>
            <TableBody>
              {e.lines.map((l) => (
                <TableRow key={l.id}><TableCell>{l.lineNumber}</TableCell><TableCell><span dir="ltr">{l.accountCode}</span> - {l.accountName}</TableCell><TableCell>{l.description}</TableCell><TableCell>{l.branchName}</TableCell><TableCell>{l.debit ? formatMoney(l.debit) : ''}</TableCell><TableCell>{l.credit ? formatMoney(l.credit) : ''}</TableCell></TableRow>
              ))}
            </TableBody>
            <TableFooter><TableRow><TableCell colSpan={4}>{t('common.total')}</TableCell><TableCell>{formatMoney(e.totalDebit, e.currencyCode)}</TableCell><TableCell>{formatMoney(e.totalCredit, e.currencyCode)}</TableCell></TableRow></TableFooter>
          </Table>
        </Box>
      </Section>
      <ReasonDialog open={dialog === 'reject'} danger title={t('common.reject')} onClose={() => setDialog(null)} onSubmit={(reason) => void run(() => api.post(`${url}/reject`, { reason }))} />
      <ReasonDialog open={dialog === 'reverse'} danger title={tx('عكس القيد (قيد عكسي بتاريخ اليوم)', 'Reverse (a reversing entry dated today)')} onClose={() => setDialog(null)} onSubmit={(reason) => void run(() => api.post(`${url}/reverse`, { reason, reversalDate: null }))} />
    </>
  );
}
