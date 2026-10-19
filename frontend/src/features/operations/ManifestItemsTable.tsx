import type { ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material';
import { useTranslation } from 'react-i18next';
import type { ManifestShipmentDto } from '@/api/generated';
import StatusChip from '@/components/common/StatusChip';
import { useTx } from '@/i18n';
import { formatMoney, formatNumber } from '@/utils/format';

/** Shipments of a transfer / manifest. extra adds columns (result, actions). */
export default function ManifestItemsTable<T extends ManifestShipmentDto>({ items, extraHead, extra }: { items: T[]; extraHead?: ReactNode; extra?: (item: T) => ReactNode }) {
  const { t } = useTranslation();
  const tx = useTx();
  const navigate = useNavigate();
  return (
    <Table size="small">
      <TableHead>
        <TableRow>
          <TableCell>#</TableCell>
          <TableCell>{tx('رقم التتبع', 'Tracking no.')}</TableCell>
          <TableCell>{t('common.status')}</TableCell>
          <TableCell>{tx('المستلم', 'Recipient')}</TableCell>
          <TableCell>{tx('الوجهة', 'Destination')}</TableCell>
          <TableCell>{tx('طرود / وزن', 'Pcs / kg')}</TableCell>
          <TableCell>COD</TableCell>
          {extraHead}
        </TableRow>
      </TableHead>
      <TableBody>
        {items.map((item, index) => (
          <TableRow key={item.itemId} hover>
            <TableCell>{index + 1}</TableCell>
            <TableCell dir="ltr" sx={{ cursor: 'pointer', color: 'primary.main', fontWeight: 600 }} onClick={() => navigate(`/shipments/${item.shipmentId}`)}>{item.trackingNumber}</TableCell>
            <TableCell><StatusChip enumName="ShipmentStatus" value={item.shipmentStatus} /></TableCell>
            <TableCell>{item.recipientName}<br /><span dir="ltr" style={{ opacity: 0.7 }}>{item.recipientPhone}</span></TableCell>
            <TableCell>{[item.destinationCityName, item.destinationZoneName].filter(Boolean).join(' / ')}</TableCell>
            <TableCell>{item.packageCount} / {formatNumber(item.chargeableWeight, 2)}</TableCell>
            <TableCell>{item.isCOD ? formatMoney(item.codAmount) : '-'}</TableCell>
            {extra?.(item)}
          </TableRow>
        ))}
        {items.length === 0 && <TableRow><TableCell colSpan={8}>{t('common.noData')}</TableCell></TableRow>}
      </TableBody>
    </Table>
  );
}
