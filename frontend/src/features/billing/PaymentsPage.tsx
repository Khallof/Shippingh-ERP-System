import { useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import type { CustomerPaymentListItemDto, PaymentMethodDto } from '@/api/generated';
import type { PagedResult } from '@/api/types';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { localInputToUtc } from '@/utils/format';
import { useTx } from '@/i18n';

type Row = CustomerPaymentListItemDto & GridValidRowModel;

/**
 * Customer payments. A new payment is allocated automatically to the oldest open invoices (or to the invoice it was
 * opened from). Sent with an Idempotency-Key: a double click never records two payments.
 */
export default function PaymentsPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const [searchParams, setSearchParams] = useSearchParams();
  const preset = useMemo(
    () => ({ customerId: Number(searchParams.get('customerId')) || null, invoiceId: Number(searchParams.get('invoiceId')) || null, amount: Number(searchParams.get('amount')) || null, currencyId: Number(searchParams.get('currencyId')) || null }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    []
  );

  // The reference (transfer / cheque / card receipt number) is asked only for the methods that require it.
  const methods = useQuery({
    queryKey: ['/payment-methods', 'payment-form'],
    queryFn: () => api.get<PagedResult<PaymentMethodDto>>('/payment-methods', { isActive: true, pageSize: 100 })
  });
  const needsReference = (methodId: unknown) =>
    !!methodId && !!methods.data?.items.find((m) => m.id === Number(methodId))?.requiresReference;

  const autoOpen = useMemo(() => !!searchParams.get('new'), []); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (autoOpen) {
      // Opened from an invoice: clean the URL; the dialog opens with the invoice values.
      const next = new URLSearchParams(searchParams);
      ['new', 'customerId', 'invoiceId', 'amount', 'currencyId'].forEach((key) => next.delete(key));
      setSearchParams(next, { replace: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <ResourcePage<Row>
      config={{
        title: t('nav.payments'),
        url: '/customer-payments',
        managePermission: P.Payments.Create,
        autoOpenCreate: autoOpen,
        defaultSort: 'paymentDateUtc',
        createLabel: tx('دفعة جديدة', 'New payment'),
        columns: [
          col.text('paymentNumber', tx('الرقم', 'Number'), { ...S, minWidth: 130 }),
          col.dateTime('paymentDateUtc', t('common.date'), S),
          col.text('customerName', t('common.customer'), { minWidth: 170 }),
          col.text('paymentMethodName', tx('طريقة الدفع', 'Method')),
          col.text('referenceNumber', tx('المرجع', 'Reference')),
          col.text('branchName', t('common.branch')),
          col.money('amount', t('common.amount'), 'currencyCode', S),
          col.money('allocatedAmount', tx('الموزّع', 'Allocated'), 'currencyCode'),
          col.money('unallocatedAmount', tx('غير الموزّع', 'Unallocated'), 'currencyCode', S)
        ],
        filters: [
          { name: 'customerId', label: t('common.customer'), type: 'lookup', lookup: 'customers' },
          { name: 'paymentMethodId', label: tx('طريقة الدفع', 'Method'), type: 'lookup', lookup: 'paymentMethods' },
          { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
          { name: 'hasUnallocatedAmount', label: tx('غير موزّعة', 'Unallocated'), type: 'boolean' }
        ],
        form: {
          idempotent: true,
          fields: [
            { name: 'customerId', label: t('common.customer'), type: 'lookup', lookup: 'customers', required: true, span: 12 },
            { name: 'paymentMethodId', label: tx('طريقة الدفع', 'Payment method'), type: 'lookup', lookup: 'paymentMethods', required: true },
            { name: 'amount', label: t('common.amount'), type: 'money', required: true, min: 0.01 },
            // The payment's currency must be the invoices' currency (empty = the base currency).
            { name: 'currencyId', label: t('common.currency'), type: 'lookup', lookup: 'currencies', helperText: tx('عملة الدين الذي تُسدّده (فارغ = العملة الأساسية). الدفعة تُنقص دين العميل بهذه العملة.', 'Currency of the debt being paid (empty = base). The payment reduces the debt in this currency.') },
            { name: 'paymentDateUtc', label: tx('وقت الدفع', 'Paid at'), type: 'datetime' },
            {
              name: 'referenceNumber',
              label: tx('رقم المرجع (رقم الحوالة / الشيك / إيصال البطاقة)', 'Reference (transfer / cheque / card receipt)'),
              required: true,
              hidden: (v) => !needsReference(v.paymentMethodId)
            },
            { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
            { name: 'notes', label: t('common.notes'), type: 'textarea' }
          ],
          toValues: () => ({ customerId: preset.customerId, paymentMethodId: null, amount: preset.amount, currencyId: preset.currencyId, paymentDateUtc: null, referenceNumber: null, branchId: null, notes: null }),
          toRequest: ({ paymentDateUtc, ...values }) => ({
            ...values,
            referenceNumber: needsReference(values.paymentMethodId) ? values.referenceNumber : null,
            paymentDateUtc: localInputToUtc(paymentDateUtc as string | null),
            autoAllocate: !preset.invoiceId,
            // From an invoice: allocate at most its balance; anything above stays unallocated on the customer's account.
            allocations: preset.invoiceId ? [{ invoiceId: preset.invoiceId, amount: Math.min(Number(values.amount), preset.amount ?? Number(values.amount)) }] : []
          }),
          canEdit: () => false
        }
      }}
    />
  );
}
