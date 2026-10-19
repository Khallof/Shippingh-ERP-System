import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import type { GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import type { InvoiceListItemDto } from '@/api/generated';
import { Permissions as P } from '@/auth/permissions.generated';
import ResourcePage from '@/components/data/ResourcePage';
import { col, S } from '@/components/data/columns';
import { useTx } from '@/i18n';
import { businessDayEndUtc, businessDayStartUtc } from '@/utils/format';

type Row = InvoiceListItemDto & GridValidRowModel;

/** Invoices: list with export, and a consolidated invoice for a customer and a period (all eligible shipments). */
export default function InvoicesPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const navigate = useNavigate();
  const config = useMemo(() => ({
    title: t('nav.invoices'),
    url: '/invoices',
    managePermission: P.Invoices.Create,
    defaultSort: 'createdAtUtc',
    rowLink: (row: Row) => `/invoices/${row.id}`,
    exportUrl: '/invoices/export',
    createLabel: tx('فاتورة مجمّعة', 'Consolidated invoice'),
    searchPlaceholder: tx('رقم الفاتورة أو العميل', 'Invoice no. or customer'),
    columns: [
      col.text<Row>('invoiceNumber', tx('الرقم', 'Number'), { ...S, minWidth: 140, valueFormatter: (v: string | null) => v ?? tx('(مسودة)', '(draft)') }),
      col.status<Row>('status', t('common.status'), 'InvoiceStatus', S),
      col.text<Row>('customerName', t('common.customer'), { minWidth: 170 }),
      col.text<Row>('branchName', t('common.branch')),
      col.date<Row>('invoiceDateUtc', tx('التاريخ', 'Date'), S),
      col.date<Row>('dueDateUtc', tx('الاستحقاق', 'Due'), S),
      col.number<Row>('lineCount', tx('الشحنات', 'Shipments')),
      col.money<Row>('totalAmount', t('common.total'), 'currencyCode', S),
      col.money<Row>('paidAmount', tx('المدفوع', 'Paid'), 'currencyCode'),
      col.money<Row>('balanceDue', tx('المتبقي', 'Balance'), 'currencyCode', S)
    ],
    filters: [
      { name: 'status', label: t('common.status'), type: 'enum' as const, enumName: 'InvoiceStatus' as const },
      { name: 'customerId', label: t('common.customer'), type: 'lookup' as const, lookup: 'customers' as const },
      { name: 'branchId', label: t('common.branch'), type: 'lookup' as const, lookup: 'branches' as const },
      { name: 'hasBalance', label: tx('عليها رصيد', 'Has balance'), type: 'boolean' as const }
    ],
    form: {
      maxWidth: 'sm' as const,
      fields: [
        { name: 'customerId', label: t('common.customer'), type: 'lookup' as const, lookup: 'customers' as const, required: true, span: 12 },
        { name: 'from', label: t('common.fromDate'), type: 'date' as const, required: true },
        { name: 'to', label: t('common.toDate'), type: 'date' as const, required: true },
        { name: 'paymentTermsDays', label: tx('مدة السداد (أيام)', 'Payment terms (days)'), type: 'integer' as const, min: 0 },
        { name: 'issueImmediately', label: tx('إصدار فوراً', 'Issue now'), type: 'checkbox' as const },
        { name: 'notes', label: t('common.notes'), type: 'textarea' as const }
      ],
      toValues: () => ({ customerId: null, from: null, to: null, paymentTermsDays: null, issueImmediately: false, notes: null }),
      toRequest: ({ from, to, ...values }: Record<string, unknown>) => ({
        ...values,
        branchId: null,
        shipmentIds: [],
        fromUtc: businessDayStartUtc(String(from)),
        toUtc: businessDayEndUtc(String(to))
      }),
      canEdit: () => false
    }
  }), [t, tx]);

  return <ResourcePage<Row> config={{ ...config, createUrl: '/invoices/consolidated', onCreated: (created) => navigate(`/invoices/${(created as { id: number }).id}`) }} />;
}
