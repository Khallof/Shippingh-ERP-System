import { useNavigate } from 'react-router-dom';
import { Button } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { JournalEntryListItemDto } from '@/api/generated';
import { Can } from '@/auth/guards';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';

type Row = JournalEntryListItemDto & GridValidRowModel;

/** Journal entries: manual entries (Draft -> Submitted -> Approved -> Posted) and automatic ones (posted by events). */
export default function JournalEntriesPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const navigate = useNavigate();
  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.journalEntries'),
        url: '/accounting/journal-entries',
        defaultSort: 'entryDate',
        rowLink: (row) => `/accounting/journal-entries/${row.id}`,
        headerActions: <Can permission={P.Accounting.CreateJournal}><Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/accounting/journal-entries/new')}>{tx('قيد يدوي', 'Manual entry')}</Button></Can>,
        columns: [
          col.text('entryNumber', tx('الرقم', 'Number'), { ...S, minWidth: 130, valueFormatter: (v: string | null) => v ?? tx('(مسودة)', '(draft)') }),
          col.date('entryDate', t('common.date'), S),
          col.enumText('journalType', tx('اليومية', 'Journal'), 'JournalType'),
          col.status('status', t('common.status'), 'JournalEntryStatus'),
          col.text('description', tx('البيان', 'Description'), { minWidth: 220 }),
          col.text('sourceNumber', tx('المستند المصدر', 'Source')),
          col.text('branchName', t('common.branch')),
          col.money('totalDebit', tx('المبلغ', 'Amount'), 'currencyCode', S),
          col.bool('isSystemGenerated', tx('آلي', 'Auto'))
        ],
        filters: [
          { name: 'status', label: t('common.status'), type: 'enum', enumName: 'JournalEntryStatus' },
          { name: 'journalType', label: tx('اليومية', 'Journal'), type: 'enum', enumName: 'JournalType' },
          { name: 'accountId', label: tx('الحساب', 'Account'), type: 'lookup', lookup: 'accounts' },
          { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
          { name: 'isSystemGenerated', label: tx('آلي', 'Automatic'), type: 'boolean' },
          { name: 'fromDate', label: t('common.fromDate'), type: 'date' },
          { name: 'toDate', label: t('common.toDate'), type: 'date' }
        ]
      }}
    />
  );
}
