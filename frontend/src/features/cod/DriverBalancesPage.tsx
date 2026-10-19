import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Dialog, DialogContent, DialogTitle, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material';
import type { GridColDef } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api, idempotent, newIdempotencyKey } from '@/api/client';
import type { DriverBalanceDto, DriverCashMovementDto } from '@/api/generated';
import type { PagedResult } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import { useNotify } from '@/components/common/Notifier';
import DataTable from '@/components/data/DataTable';
import FilterBar from '@/components/data/FilterBar';
import { col, S } from '@/components/data/columns';
import { useApiList } from '@/components/data/useApiList';
import { useListState } from '@/components/data/useListState';
import FormDialog from '@/components/form/FormDialog';
import { useTx } from '@/i18n';
import { formatDateTime, formatMoney } from '@/utils/format';

/**
 * Driver cash balances (COD collected and not yet handed over). Handover = a receipt voucher into a cashbox; a
 * shortage or excess is recorded on the voucher. Movements = the driver's cash ledger.
 */
export default function DriverBalancesPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const state = useListState({ defaultSort: 'balance' });
  const list = useApiList<DriverBalanceDto>('/cod/driver-balances', { onlyOutstanding: true, ...state.params });
  const [handover, setHandover] = useState<{ row: DriverBalanceDto; key: string } | null>(null);
  const [movements, setMovements] = useState<DriverBalanceDto | null>(null);
  const moves = useQuery({
    queryKey: ['/cod', 'movements', movements?.driverId],
    queryFn: () => api.get<PagedResult<DriverCashMovementDto>>(`/cod/driver-balances/${movements!.driverId}/movements`, { pageSize: 100 }),
    enabled: !!movements
  });

  const columns: GridColDef<DriverBalanceDto>[] = [
    col.text('employeeCode', tx('الرمز', 'Code'), { flex: 0, width: 100 }),
    col.text('driverName', t('common.driver'), { ...S, minWidth: 160 }),
    col.text('branchName', t('common.branch')),
    col.money('balance', tx('العهدة', 'Balance'), 'currencyCode', S),
    col.number('unhandedCodCount', tx('شحنات لم تُسلّم نقديتها', 'Unhanded COD')),
    col.dateTime('oldestUnhandedCollectionAtUtc', tx('أقدم تحصيل', 'Oldest collection')),
    col.dateTime('lastHandoverAtUtc', tx('آخر تسليم', 'Last handover')),
    {
      field: '__actions', headerName: t('common.actions'), width: 220, renderCell: ({ row }) => (
        <>
          <Button size="small" onClick={() => setMovements(row)}>{tx('الحركات', 'Movements')}</Button>
          {hasPermission(P.Cod.Handover) && row.balance > 0 && <Button size="small" variant="contained" onClick={() => setHandover({ row, key: newIdempotencyKey() })}>{tx('استلام العهدة', 'Receive cash')}</Button>}
        </>
      )
    }
  ];

  return (
    <>
      <PageHeader title={t('nav.driverBalances')} />
      <FilterBar state={state} filters={[{ name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' }, { name: 'driverId', label: t('common.driver'), type: 'lookup', lookup: 'drivers' }]} />
      <DataTable<DriverBalanceDto> columns={columns} data={list.data} loading={list.isFetching} error={list.error} state={state} getRowId={(row) => `${row.driverId}-${row.currencyId}`} />

      <FormDialog<Record<string, unknown>>
        open={!!handover}
        maxWidth="sm"
        title={`${tx('استلام عهدة السائق', 'Driver cash handover')} · ${handover?.row.driverName ?? ''} (${formatMoney(handover?.row.balance, handover?.row.currencyCode)})`}
        fields={[
          { name: 'cashboxId', label: tx('الصندوق', 'Cashbox'), type: 'lookup', lookup: 'cashboxes', required: true, span: 12 },
          { name: 'amount', label: tx('المبلغ المستلم فعلاً', 'Amount received'), type: 'money', required: true, min: 0.01, helperText: tx('الفرق عن العهدة يُسجّل عجزاً أو زيادة', 'Any difference is recorded as a shortage / excess') },
          { name: 'notes', label: t('common.notes'), type: 'textarea' }
        ]}
        defaultValues={{ cashboxId: null, amount: handover?.row.balance ?? null, notes: null }}
        onClose={() => setHandover(null)}
        onSubmit={async (values) => {
          await api.post('/cod/driver-handover', { ...values, driverId: handover!.row.driverId, codTransactionIds: [] }, idempotent(handover!.key));
          notify.success(t('common.done'));
          setHandover(null);
          await queryClient.invalidateQueries({ queryKey: ['/cod'] });
        }}
      />

      <Dialog open={!!movements} onClose={() => setMovements(null)} maxWidth="md" fullWidth>
        <DialogTitle>{tx('حركات عهدة', 'Cash movements of')} {movements?.driverName}</DialogTitle>
        <DialogContent>
          <Table size="small">
            <TableHead><TableRow><TableCell>{t('common.date')}</TableCell><TableCell>{tx('الحركة', 'Movement')}</TableCell><TableCell>{tx('المستند', 'Document')}</TableCell><TableCell>{tx('مدين', 'Debit')}</TableCell><TableCell>{tx('دائن', 'Credit')}</TableCell><TableCell>{tx('الرصيد', 'Balance')}</TableCell></TableRow></TableHead>
            <TableBody>
              {moves.data?.items.map((m) => (
                <TableRow key={m.id}><TableCell>{formatDateTime(m.movementDateUtc)}</TableCell><TableCell>{m.movementTypeName}</TableCell><TableCell>{m.documentNumber}</TableCell><TableCell>{m.debit ? formatMoney(m.debit) : ''}</TableCell><TableCell>{m.credit ? formatMoney(m.credit) : ''}</TableCell><TableCell>{formatMoney(m.balanceAfter)}</TableCell></TableRow>
              ))}
            </TableBody>
          </Table>
        </DialogContent>
      </Dialog>
    </>
  );
}
