import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { IconButton, Tooltip } from '@mui/material';
import PictureAsPdfOutlinedIcon from '@mui/icons-material/PictureAsPdfOutlined';
import BlockIcon from '@mui/icons-material/Block';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api, openFile } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { Permissions as P } from '@/auth/permissions.generated';
import { ReasonDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import type { FieldDef } from '@/components/form/fields';
import { useTx } from '@/i18n';

type Row = GridValidRowModel & { id: number; status: string; voucherNumber: string };
export type VoucherKind = 'receipts' | 'payments' | 'expenses';

/**
 * Cash receipt vouchers (سند قبض), cash payment vouchers (سند صرف) and expenses (= payment vouchers of type Expense).
 * Each voucher posts to the cashbox and to accounting at once; it is never edited, only cancelled with a reason
 * (a reversing movement). The PDF voucher (amount in words, signatures) opens for printing.
 */
export default function VouchersPage({ kind }: { kind: VoucherKind }) {
  const { t } = useTranslation();
  const tx = useTx();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [cancelling, setCancelling] = useState<Row | null>(null);
  const receipts = kind === 'receipts';
  const url = receipts ? '/cash-receipts' : '/cash-payments';

  const pdf = async (row: Row) => {
    try {
      openFile(await api.download(`${url}/${row.id}/pdf`, { fallbackName: `${row.voucherNumber}.pdf` }));
    } catch (error) {
      notify.error(error);
    }
  };

  const common: FieldDef[] = [
    { name: 'cashboxId', label: tx('الصندوق', 'Cashbox'), type: 'lookup', lookup: 'cashboxes', required: true },
    { name: 'amount', label: t('common.amount'), type: 'money', required: true, min: 0.01 }
  ];
  const fields: FieldDef[] = receipts
    ? [
        ...common,
        { name: 'receiptType', label: tx('نوع القبض', 'Receipt type'), type: 'select', required: true, options: [
          { value: 'CustomerPayment', label: tx('دفعة عميل', 'Customer payment') },
          { value: 'OtherIncome', label: tx('إيراد آخر', 'Other income') },
          { value: 'Other', label: tx('أخرى', 'Other') }
        ] },
        { name: 'receivedFrom', label: tx('استلمنا من', 'Received from'), required: true },
        { name: 'customerId', label: t('common.customer'), type: 'lookup', lookup: 'customers', hidden: (v) => v.receiptType !== 'CustomerPayment' },
        { name: 'referenceNumber', label: tx('المرجع', 'Reference') },
        { name: 'description', label: tx('البيان', 'Description'), required: true, type: 'textarea' }
      ]
    : [
        ...common,
        ...(kind === 'expenses'
          ? [{ name: 'expenseCategoryId', label: tx('بند المصروف', 'Expense category'), type: 'lookup', lookup: 'expenseCategories', required: true } as FieldDef]
          : [{ name: 'paymentType', label: tx('نوع الصرف', 'Payment type'), type: 'select', required: true, options: [
              { value: 'SupplierPayment', label: tx('دفعة مورد', 'Supplier payment') },
              { value: 'EmployeeAdvance', label: tx('سلفة موظف', 'Employee advance') },
              { value: 'CustomerRefund', label: tx('رد مبلغ لعميل', 'Customer refund') },
              { value: 'Expense', label: tx('مصروف', 'Expense') },
              { value: 'Other', label: tx('أخرى', 'Other') }
            ] } as FieldDef]),
        { name: 'paidTo', label: tx('صُرف إلى', 'Paid to'), required: true },
        { name: 'supplierId', label: tx('المورد', 'Supplier'), type: 'lookup', lookup: 'suppliers', hidden: (v) => kind !== 'expenses' && v.paymentType !== 'SupplierPayment' },
        { name: 'employeeId', label: tx('الموظف', 'Employee'), type: 'lookup', lookup: 'employees', hidden: (v) => v.paymentType !== 'EmployeeAdvance' },
        { name: 'customerId', label: t('common.customer'), type: 'lookup', lookup: 'customers', hidden: (v) => v.paymentType !== 'CustomerRefund' },
        { name: 'expenseCategoryId', label: tx('بند المصروف', 'Expense category'), type: 'lookup', lookup: 'expenseCategories', hidden: (v) => kind === 'expenses' || v.paymentType !== 'Expense' },
        { name: 'referenceNumber', label: tx('رقم الفاتورة / المرجع', 'Invoice / reference') },
        { name: 'description', label: tx('البيان', 'Description'), required: true, type: 'textarea' }
      ];

  const title = receipts ? t('nav.receipts') : kind === 'expenses' ? t('nav.expenses') : t('nav.cashPayments');

  return (
    <>
      <ResourcePage<Row>
        key={kind}
        config={{
          title,
          subtitle: kind === 'expenses' ? tx('المصروفات النقدية تُسجّل كسندات صرف من نوع "مصروف" وتُرحّل لحساب البند.', 'Cash expenses are payment vouchers of type "Expense" posted to the category account.') : undefined,
          url,
          fixedParams: kind === 'expenses' ? { type: 'Expense' } : undefined,
          managePermission: P.CashVouchers.Create,
          defaultSort: 'voucherDateUtc',
          createLabel: receipts ? tx('سند قبض', 'Receipt voucher') : kind === 'expenses' ? tx('مصروف جديد', 'New expense') : tx('سند صرف', 'Payment voucher'),
          columns: [
            col.text('voucherNumber', tx('الرقم', 'Number'), { ...S, minWidth: 130 }),
            col.dateTime('voucherDateUtc', t('common.date'), S),
            col.text(receipts ? 'receiptTypeName' : 'paymentTypeName', tx('النوع', 'Type')),
            col.status('status', t('common.status'), 'CashVoucherStatus'),
            col.text('cashboxName', tx('الصندوق', 'Cashbox')),
            col.text(receipts ? 'receivedFrom' : 'paidTo', receipts ? tx('من', 'From') : tx('إلى', 'To'), { minWidth: 150 }),
            ...(kind === 'expenses' ? [col.text<Row>('expenseCategoryName', tx('البند', 'Category'))] : []),
            col.text('description', tx('البيان', 'Description'), { minWidth: 180 }),
            col.money('amount', t('common.amount'), 'currencyCode', S)
          ],
          filters: [
            { name: 'cashboxId', label: tx('الصندوق', 'Cashbox'), type: 'lookup', lookup: 'cashboxes' },
            { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
            { name: 'status', label: t('common.status'), type: 'enum', enumName: 'CashVoucherStatus' },
            ...(kind === 'expenses' ? [] : [{ name: 'type', label: tx('النوع', 'Type'), type: 'enum' as const, enumName: receipts ? 'CashReceiptType' as const : 'CashPaymentType' as const }])
          ],
          rowActions: (row) => (
            <>
              <Tooltip title="PDF"><IconButton size="small" onClick={() => void pdf(row)}><PictureAsPdfOutlinedIcon fontSize="small" /></IconButton></Tooltip>
              {row.status === 'Posted' && hasPermission(P.CashVouchers.Cancel) && <Tooltip title={t('common.cancel')}><IconButton size="small" color="error" onClick={() => setCancelling(row)}><BlockIcon fontSize="small" /></IconButton></Tooltip>}
            </>
          ),
          onCreated: (created) => void pdf(created as Row),
          form: {
            idempotent: true,
            maxWidth: 'sm',
            fields,
            toValues: () => (receipts
              ? { cashboxId: null, amount: null, receiptType: 'OtherIncome', receivedFrom: '', customerId: null, referenceNumber: null, description: '' }
              : { cashboxId: null, amount: null, paymentType: kind === 'expenses' ? 'Expense' : 'SupplierPayment', paidTo: '', supplierId: null, employeeId: null, customerId: null, expenseCategoryId: null, referenceNumber: null, description: '' }),
            toRequest: (values) => (kind === 'expenses' ? { ...values, paymentType: 'Expense' } : values),
            canEdit: () => false
          }
        }}
      />
      <ReasonDialog open={!!cancelling} danger title={`${t('common.cancel')} ${cancelling?.voucherNumber ?? ''}`} onClose={() => setCancelling(null)}
        onSubmit={async (reason) => {
          try {
            await api.post(`${url}/${cancelling!.id}/cancel`, { reason });
            setCancelling(null);
            notify.success(t('common.done'));
            await queryClient.invalidateQueries({ queryKey: [url] });
          } catch (error) {
            notify.error(error);
          }
        }} />
    </>
  );
}
