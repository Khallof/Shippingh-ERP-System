import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { IconButton, Tooltip } from '@mui/material';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import MoveToInboxOutlinedIcon from '@mui/icons-material/MoveToInboxOutlined';
import BlockIcon from '@mui/icons-material/Block';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api } from '@/api/client';
import type { CashTransferDto } from '@/api/generated';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';

type Row = CashTransferDto & GridValidRowModel;
type Action = 'approve' | 'reject' | 'receive' | 'cancel';

/** Cash transfers between cashboxes / branches / to the bank: request -> approve -> (in transit) -> receive. */
export default function CashTransfersPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [action, setAction] = useState<{ row: Row; kind: Action } | null>(null);

  const submit = async (text: string) => {
    const { row, kind } = action!;
    const body = kind === 'approve' ? { bankReference: null, notes: text || null } : kind === 'receive' ? undefined : { reason: text };
    try {
      await api.post(`/cash-transfers/${row.id}/${kind}`, body);
      setAction(null);
      notify.success(t('common.done'));
      await queryClient.invalidateQueries({ queryKey: ['/cash-transfers'] });
    } catch (error) {
      notify.error(error);
    }
  };

  return (
    <>
      <ResourcePage<Row>
        config={{
          title: t('nav.cashTransfers'),
          url: '/cash-transfers',
          managePermission: P.CashTransfers.Create,
          defaultSort: 'createdAtUtc',
          createLabel: tx('تحويل جديد', 'New transfer'),
          columns: [
            col.text('transferNumber', tx('الرقم', 'Number'), { ...S, minWidth: 130 }),
            col.enumText('transferType', tx('النوع', 'Type'), 'CashTransferType'),
            col.status('status', t('common.status'), 'CashTransferStatus'),
            col.text('fromCashboxName', tx('من صندوق', 'From')),
            col.text('toCashboxName', tx('إلى', 'To'), { valueGetter: (_v, row) => row.toCashboxName ?? row.toBankAccountName }),
            col.money('amount', t('common.amount'), 'currencyCode', S),
            col.text('reason', t('common.reason')),
            col.dateTime('createdAtUtc', tx('أُنشئ', 'Created'), S)
          ],
          filters: [
            { name: 'status', label: t('common.status'), type: 'enum', enumName: 'CashTransferStatus' },
            { name: 'transferType', label: tx('النوع', 'Type'), type: 'enum', enumName: 'CashTransferType' },
            { name: 'cashboxId', label: tx('الصندوق', 'Cashbox'), type: 'lookup', lookup: 'cashboxes' }
          ],
          rowActions: (row) => (
            <>
              {row.status === 'PendingApproval' && hasPermission(P.CashTransfers.Approve) && (
                <>
                  <Tooltip title={t('common.approve')}><IconButton size="small" color="success" onClick={() => setAction({ row, kind: 'approve' })}><CheckIcon fontSize="small" /></IconButton></Tooltip>
                  <Tooltip title={t('common.reject')}><IconButton size="small" color="error" onClick={() => setAction({ row, kind: 'reject' })}><CloseIcon fontSize="small" /></IconButton></Tooltip>
                </>
              )}
              {row.status === 'InTransit' && hasPermission(P.CashTransfers.Receive) && <Tooltip title={t('common.receive')}><IconButton size="small" onClick={() => setAction({ row, kind: 'receive' })}><MoveToInboxOutlinedIcon fontSize="small" /></IconButton></Tooltip>}
              {row.status === 'PendingApproval' && hasPermission(P.CashTransfers.Create) && <Tooltip title={t('common.cancel')}><IconButton size="small" onClick={() => setAction({ row, kind: 'cancel' })}><BlockIcon fontSize="small" /></IconButton></Tooltip>}
            </>
          ),
          form: {
            maxWidth: 'sm',
            fields: [
              { name: 'fromCashboxId', label: tx('من صندوق', 'From cashbox'), type: 'lookup', lookup: 'cashboxes', required: true, span: 12 },
              { name: 'toCashboxId', label: tx('إلى صندوق', 'To cashbox'), type: 'lookup', lookup: 'cashboxes', disabled: (v) => !!v.toBankAccountId },
              { name: 'toBankAccountId', label: tx('أو إيداع في حساب بنكي', 'Or deposit to bank'), type: 'lookup', lookup: 'bankAccounts', disabled: (v) => !!v.toCashboxId },
              { name: 'amount', label: t('common.amount'), type: 'money', required: true, min: 0.01 },
              { name: 'bankReference', label: tx('مرجع الإيداع', 'Bank reference') },
              { name: 'reason', label: t('common.reason'), required: true, type: 'textarea' }
            ],
            toValues: () => ({ fromCashboxId: null, toCashboxId: null, toBankAccountId: null, amount: null, bankReference: null, reason: '' }),
            canEdit: () => false
          }
        }}
      />
      <ReasonDialog open={!!action} required={action?.kind === 'reject' || action?.kind === 'cancel'} danger={action?.kind === 'reject' || action?.kind === 'cancel'}
        title={action ? { approve: t('common.approve'), reject: t('common.reject'), receive: t('common.receive'), cancel: t('common.cancel') }[action.kind] : ''}
        label={action?.kind === 'reject' || action?.kind === 'cancel' ? t('common.reason') : t('common.notes')}
        onClose={() => setAction(null)} onSubmit={(text) => void submit(text)} />
    </>
  );
}
