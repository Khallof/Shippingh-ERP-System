import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Table, TableBody, TableCell, TableHead, TableRow, Checkbox } from '@mui/material';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import { toAppError, type AppError } from '@/api/errors';
import type { SenderSettlementDto, SenderSettlementListItemDto, SettlementCandidateDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import LookupSelect from '@/components/form/LookupSelect';
import { useTx } from '@/i18n';
import { formatDateTime, formatMoney } from '@/utils/format';

type Row = SenderSettlementListItemDto & GridValidRowModel;

/** New settlement: choose a sender, the ready COD lines are listed (all selected), create a draft settlement. */
function NewSettlementDialog({ onClose }: { onClose(): void }) {
  const { t } = useTranslation();
  const tx = useTx();
  const navigate = useNavigate();
  const [sender, setSender] = useState<number | null>(null);
  const [excluded, setExcluded] = useState<Set<number>>(new Set());
  const [error, setError] = useState<AppError | null>(null);
  const [busy, setBusy] = useState(false);
  const candidates = useQuery({
    queryKey: ['/cod/settlements', 'candidates', sender],
    queryFn: () => api.get<SettlementCandidateDto[]>('/cod/settlements/candidates', { senderCustomerId: sender }),
    enabled: !!sender
  });
  const rows = candidates.data ?? [];
  const chosen = rows.filter((row) => !excluded.has(row.codTransactionId));
  const net = chosen.reduce((sum, row) => sum + row.remainingPayable, 0);

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const created = await api.post<SenderSettlementDto>('/cod/settlements', { senderCustomerId: sender, codTransactionIds: chosen.map((row) => row.codTransactionId), lines: [], notes: null });
      navigate(`/cod/settlements/${created.id}`);
    } catch (caught) {
      setError(await toAppError(caught));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>{tx('تسوية مرسل جديدة', 'New sender settlement')}</DialogTitle>
      <DialogContent dividers>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error.message}</Alert>}
        <LookupSelect source="senders" label={tx('المرسل', 'Sender')} value={sender} onChange={(v) => { setSender(v as number | null); setExcluded(new Set()); }} required />
        {sender && !candidates.isLoading && rows.length === 0 && <Alert severity="info" sx={{ mt: 2 }}>{tx('لا توجد مبالغ جاهزة للتسوية لهذا المرسل.', 'Nothing is ready for settlement for this sender.')}</Alert>}
        {rows.length > 0 && (
          <Table size="small" sx={{ mt: 2 }}>
            <TableHead><TableRow><TableCell padding="checkbox" /><TableCell>{tx('رقم التتبع', 'Tracking no.')}</TableCell><TableCell>{tx('المحصّل', 'Collected')}</TableCell><TableCell>{tx('الخصومات', 'Deductions')}</TableCell><TableCell>{tx('المستحق', 'Payable')}</TableCell><TableCell>{tx('التحصيل', 'Collected at')}</TableCell></TableRow></TableHead>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.codTransactionId}>
                  <TableCell padding="checkbox"><Checkbox checked={!excluded.has(row.codTransactionId)} onChange={(e) => setExcluded((current) => { const next = new Set(current); if (e.target.checked) next.delete(row.codTransactionId); else next.add(row.codTransactionId); return next; })} /></TableCell>
                  <TableCell dir="ltr">{row.trackingNumber}</TableCell>
                  <TableCell>{formatMoney(row.collectedAmount)}</TableCell>
                  <TableCell>{formatMoney(row.shippingChargeDeduction + row.codCommissionDeduction + row.taxDeduction + row.otherFeesAmount - row.compensationAmount)}</TableCell>
                  <TableCell><strong>{formatMoney(row.remainingPayable)}</strong></TableCell>
                  <TableCell>{formatDateTime(row.collectedAtUtc)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions>
        <span style={{ flex: 1, paddingInline: 16 }}>{tx('الصافي', 'Net')}: <strong>{formatMoney(net)}</strong> ({chosen.length})</span>
        <Button onClick={onClose}>{t('common.cancel')}</Button>
        <Button variant="contained" disabled={busy || chosen.length === 0} onClick={() => void create()}>{t('common.create')}</Button>
      </DialogActions>
    </Dialog>
  );
}

export default function SettlementsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const { hasPermission } = useAuth();
  const [creating, setCreating] = useState(false);
  return (
    <>
      <ResourcePage<Row>
        config={{
          title: t('nav.settlements'),
          url: '/cod/settlements',
          defaultSort: 'settlementDateUtc',
          rowLink: (row) => `/cod/settlements/${row.id}`,
          headerActions: hasPermission(P.Cod.Settle) && <Button variant="contained" onClick={() => setCreating(true)}>{tx('تسوية جديدة', 'New settlement')}</Button>,
          columns: [
            col.text('settlementNumber', tx('الرقم', 'Number'), { ...S, minWidth: 140 }),
            col.status('status', t('common.status'), 'SettlementStatus'),
            col.dateTime('settlementDateUtc', t('common.date'), S),
            col.text('senderName', tx('المرسل', 'Sender'), { minWidth: 160 }),
            col.text('branchName', t('common.branch')),
            col.number('lineCount', tx('الشحنات', 'Shipments')),
            col.money('totalGrossReleased', tx('الإجمالي', 'Gross'), 'currencyCode'),
            col.money('totalDeductions', tx('الخصومات', 'Deductions'), 'currencyCode'),
            col.money('netPayable', tx('الصافي', 'Net'), 'currencyCode', S),
            col.dateTime('paidAtUtc', tx('دُفعت', 'Paid at'))
          ],
          filters: [
            { name: 'status', label: t('common.status'), type: 'enum', enumName: 'SettlementStatus' },
            { name: 'senderCustomerId', label: tx('المرسل', 'Sender'), type: 'lookup', lookup: 'senders' },
            { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' }
          ]
        }}
      />
      {creating && <NewSettlementDialog onClose={() => setCreating(false)} />}
    </>
  );
}
