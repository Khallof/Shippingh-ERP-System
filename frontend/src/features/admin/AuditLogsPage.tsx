import { useMemo, useState } from 'react';
import { Dialog, DialogContent, DialogTitle, Typography } from '@mui/material';
import type { GridColDef } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { AuditLogDto } from '@/api/generated';
import PageHeader from '@/components/common/PageHeader';
import InfoGrid from '@/components/common/InfoGrid';
import DataTable from '@/components/data/DataTable';
import FilterBar from '@/components/data/FilterBar';
import { col, S } from '@/components/data/columns';
import { useApiList } from '@/components/data/useApiList';
import { useListState } from '@/components/data/useListState';
import { enumLabel } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';
import { businessDayEndUtc, businessDayStartUtc, formatDateTime } from '@/utils/format';

/** Read-only audit log (append-only on the server): who did what, when, from where; details open in a dialog. */
export default function AuditLogsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const state = useListState({ defaultSort: 'createdAtUtc', defaultPageSize: 50 });
  const [open, setOpen] = useState<AuditLogDto | null>(null);
  const params = useMemo(() => {
    const { from, to, ...rest } = state.params as Record<string, string | number>;
    return { ...rest, fromUtc: from ? businessDayStartUtc(String(from)) : undefined, toUtc: to ? businessDayEndUtc(String(to)) : undefined };
  }, [state.params]);
  const list = useApiList<AuditLogDto>('/audit-logs', params);

  const columns: GridColDef<AuditLogDto>[] = [
    col.dateTime('createdAtUtc', tx('الوقت', 'Time'), S),
    col.text('action', tx('العملية', 'Action'), { ...S, minWidth: 180, valueFormatter: (v: string) => enumLabel('AuditAction', v, language) }),
    col.bool('succeeded', tx('نجحت', 'OK')),
    col.text('userName', tx('المستخدم', 'User'), S),
    col.text('entityName', tx('الكيان', 'Entity')),
    col.text('entityId', tx('المعرّف', 'Id'), { flex: 0, width: 100 }),
    col.text('ipAddress', 'IP', { flex: 0, width: 130 }),
    col.text('details', tx('التفاصيل', 'Details'), { minWidth: 240 })
  ];

  return (
    <>
      <PageHeader title={t('nav.auditLogs')} />
      <FilterBar state={state} searchPlaceholder={tx('المستخدم أو الكيان', 'User or entity')} filters={[
        { name: 'action', label: tx('العملية', 'Action'), type: 'enum', enumName: 'AuditAction', span: 3 },
        { name: 'succeeded', label: tx('نجحت', 'Succeeded'), type: 'boolean' },
        { name: 'entityName', label: tx('الكيان', 'Entity'), type: 'text' },
        { name: 'from', label: t('common.fromDate'), type: 'date' },
        { name: 'to', label: t('common.toDate'), type: 'date' }
      ]} />
      <DataTable<AuditLogDto> columns={columns} data={list.data} loading={list.isFetching} error={list.error} state={state} onRowClick={setOpen} />
      <Dialog open={!!open} onClose={() => setOpen(null)} maxWidth="md" fullWidth>
        <DialogTitle>{open && enumLabel('AuditAction', open.action, language)}</DialogTitle>
        <DialogContent>
          {open && <InfoGrid items={[{ label: tx('الوقت', 'Time'), value: formatDateTime(open.createdAtUtc) }, { label: tx('المستخدم', 'User'), value: open.userName }, { label: 'IP', value: open.ipAddress }, { label: tx('المتصفح', 'User agent'), value: open.userAgent, span: 12 }]} />}
          <Typography component="pre" dir="ltr" sx={{ mt: 2, whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: 13 }}>{open?.details}</Typography>
        </DialogContent>
      </Dialog>
    </>
  );
}
