import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, IconButton, InputAdornment, TextField, Tooltip } from '@mui/material';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import AddIcon from '@mui/icons-material/Add';
import PrintOutlinedIcon from '@mui/icons-material/PrintOutlined';
import QrCodeScannerIcon from '@mui/icons-material/QrCodeScanner';
import type { GridColDef } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { openFile } from '@/api/client';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { ShipmentListItemDto } from '@/api/generated';
import { Can } from '@/auth/guards';
import { useAuth } from '@/auth/AuthProvider';
import { ConfirmDialog } from '@/components/common/Dialogs';
import { Permissions as P } from '@/auth/permissions.generated';
import PageHeader from '@/components/common/PageHeader';
import ExportMenu from '@/components/common/ExportMenu';
import { useNotify } from '@/components/common/Notifier';
import DataTable from '@/components/data/DataTable';
import FilterBar, { type FilterDef } from '@/components/data/FilterBar';
import { col, S } from '@/components/data/columns';
import { useApiList } from '@/components/data/useApiList';
import { useListState } from '@/components/data/useListState';
import { useTx } from '@/i18n';
import { businessDayEndUtc, businessDayStartUtc } from '@/utils/format';

/**
 * Shipment list: server paging / sorting / search, filters kept in the URL, Excel / PDF export with the same
 * filters, bulk label printing of the selected rows and a scan box that opens a shipment by tracking number.
 */
/** Shipments that can still be deleted: not yet sent out of the origin branch (same rule as cancelling). */
const deletableStatuses: string[] = ['Created', 'PickedUp', 'ReceivedAtBranch', 'InWarehouse'];

export default function ShipmentListPage() {
  const { t } = useTranslation();
  const tx = useTx();
  const navigate = useNavigate();
  const notify = useNotify();
  const state = useListState({ defaultSort: 'createdAtUtc' });
  const [selected, setSelected] = useState<number[]>([]);
  const [scan, setScan] = useState('');
  const [deleting, setDeleting] = useState<ShipmentListItemDto | null>(null);
  const [busy, setBusy] = useState(false);
  const { hasPermission } = useAuth();
  const canDelete = hasPermission(P.Shipments.Cancel);

  // The date filters are business days in the URL; the API wants UTC instants.
  const params = useMemo(() => {
    const { createdFrom, createdTo, ...rest } = state.params as Record<string, string | number>;
    return {
      ...rest,
      // Deleted (cancelled) shipments are hidden unless the "Cancelled" status is chosen in the filter.
      excludeCancelled: rest.status ? undefined : true,
      createdFromUtc: createdFrom ? businessDayStartUtc(String(createdFrom)) : undefined,
      createdToUtc: createdTo ? businessDayEndUtc(String(createdTo)) : undefined
    };
  }, [state.params]);

  const list = useApiList<ShipmentListItemDto>('/shipments', params);

  const filters: FilterDef[] = [
    { name: 'status', label: t('common.status'), type: 'enum', enumName: 'ShipmentStatus' },
    { name: 'branchId', label: t('common.branch'), type: 'lookup', lookup: 'branches' },
    { name: 'senderCustomerId', label: tx('المرسل', 'Sender'), type: 'lookup', lookup: 'senders' },
    { name: 'driverId', label: t('common.driver'), type: 'lookup', lookup: 'drivers' },
    { name: 'serviceTypeId', label: tx('نوع البضاعة', 'Goods type'), type: 'lookup', lookup: 'serviceTypes' },
    { name: 'isCOD', label: tx('دفع عند الاستلام', 'COD'), type: 'boolean' },
    { name: 'createdFrom', label: t('common.fromDate'), type: 'date' },
    { name: 'createdTo', label: t('common.toDate'), type: 'date' }
  ];

  const columns: GridColDef<ShipmentListItemDto>[] = [
    {
      field: 'actions',
      headerName: '',
      width: 60,
      sortable: false,
      filterable: false,
      renderCell: ({ row }) =>
        canDelete && deletableStatuses.includes(row.status) ? (
          <Tooltip title={tx('حذف الشحنة', 'Delete shipment')}>
            <IconButton size="small" color="error" onClick={(event) => { event.stopPropagation(); setDeleting(row); }}>
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        ) : null
    },
    col.text('trackingNumber', tx('رقم التتبع', 'Tracking no.'), { ...S, minWidth: 150, renderCell: ({ value }) => <span dir="ltr" style={{ fontWeight: 600 }}>{value}</span> }),
    col.text('shipmentNumber', tx('رقم الشحنة', 'Shipment no.'), { ...S, minWidth: 130 }),
    col.status('status', t('common.status'), 'ShipmentStatus', S),
    col.text('senderName', tx('المرسل', 'Sender'), { minWidth: 150 }),
    col.text('recipientName', tx('المستلم', 'Recipient'), { minWidth: 150, renderCell: ({ row }) => `${row.recipientName} · ${row.recipientPhone}` }),
    col.text('destinationCityName', tx('مدينة الوجهة', 'Destination'), { minWidth: 110 }),
    col.text('currentBranchName', tx('الفرع الحالي', 'Current branch'), { minWidth: 120 }),
    col.text('assignedDriverName', t('common.driver'), { minWidth: 120 }),
    col.number('packageCount', tx('طرود', 'Pcs'), 0, { width: 70 }),
    col.number('chargeableWeight', tx('الوزن', 'Weight'), 2, { ...S, width: 90 }),
    col.money('codAmount', tx('مبلغ التحصيل', 'COD'), 'currencyCode', S),
    col.money('totalCharge', tx('الأجور', 'Charges'), 'currencyCode', S),
    col.dateTime('createdAtUtc', tx('تاريخ التسجيل', 'Created'), S)
  ];

  const deleteShipment = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      await shipmentsApi.cancel(deleting.id, { reason: tx('حُذفت من قائمة الشحنات', 'Deleted from the shipment list') });
      notify.success(`${tx('تم حذف الشحنة', 'Shipment deleted')} ${deleting.trackingNumber}`);
      setDeleting(null);
      await list.refetch();
    } catch (error) {
      notify.error(error);
    } finally {
      setBusy(false);
    }
  };

  const printLabels = async () => {
    try {
      openFile(await shipmentsApi.labels(selected));
    } catch (error) {
      notify.error(error);
    }
  };

  const openScanned = async () => {
    const code = scan.trim();
    if (!code) return;
    try {
      const found = await shipmentsApi.track(code);
      setScan('');
      navigate(`/shipments/${found.shipmentId}`);
    } catch (error) {
      notify.error(error);
    }
  };

  return (
    <>
      <PageHeader
        title={t('nav.shipments')}
        subtitle={list.data ? `${list.data.totalCount} ${tx('شحنة', 'shipments')}` : undefined}
        actions={
          <>
            <TextField
              placeholder={tx('امسح الباركود / رقم التتبع', 'Scan barcode / tracking no.')}
              value={scan}
              onChange={(event) => setScan(event.target.value)}
              onKeyDown={(event) => event.key === 'Enter' && void openScanned()}
              sx={{ width: 260 }}
              inputProps={{ dir: 'ltr' }}
              InputProps={{ startAdornment: <InputAdornment position="start"><QrCodeScannerIcon fontSize="small" /></InputAdornment> }}
            />
            <Button variant="outlined" startIcon={<PrintOutlinedIcon />} disabled={selected.length === 0 || selected.length > 100} onClick={() => void printLabels()}>
              {tx('طباعة الملصقات', 'Print labels')} {selected.length > 0 ? `(${selected.length})` : ''}
            </Button>
            <ExportMenu url="/shipments/export" params={params} />
            <Can permission={P.Shipments.Create}>
              <Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/shipments/new')}>
                {t('nav.newShipment')}
              </Button>
            </Can>
          </>
        }
      />
      <FilterBar state={state} filters={filters} searchPlaceholder={tx('رقم، اسم أو هاتف', 'Number, name or phone')} />
      <DataTable<ShipmentListItemDto>
        columns={columns}
        data={list.data}
        loading={list.isFetching}
        error={list.error}
        onRetry={() => void list.refetch()}
        state={state}
        rowLink={(row) => `/shipments/${row.id}`}
        checkboxSelection
        onSelectionChange={(ids) => setSelected(ids.map(Number))}
      />
      <ConfirmDialog
        open={!!deleting}
        danger
        busy={busy}
        title={`${tx('حذف الشحنة', 'Delete shipment')} ${deleting?.trackingNumber ?? ''}`}
        message={tx(
          'ستُحذف الشحنة من القائمة ويُلغى دينها عن العميل. لا يمكن التراجع عن ذلك. (الشحنة التي سُجّلت عليها دفعة لا يمكن حذفها.)',
          'The shipment is removed from the list and its debt is cancelled. This cannot be undone. (A shipment with a payment cannot be deleted.)'
        )}
        confirmText={tx('حذف', 'Delete')}
        onConfirm={() => void deleteShipment()}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}
