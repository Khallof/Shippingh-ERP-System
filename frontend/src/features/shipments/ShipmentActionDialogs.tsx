import { useState } from 'react';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Grid2 as Grid, MenuItem, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { AppError, toAppError } from '@/api/errors';
import type { ShipmentDto, ShipmentStatus } from '@/api/generated';
import LookupSelect from '@/components/form/LookupSelect';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { useLanguage, useTx } from '@/i18n';

interface BaseProps {
  open: boolean;
  shipment: ShipmentDto;
  onClose(): void;
  onSubmit(values: Record<string, unknown>): Promise<unknown>;
}

function useSubmit(onSubmit: BaseProps['onSubmit']) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<AppError | null>(null);
  const run = async (values: Record<string, unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await onSubmit(values);
    } catch (caught) {
      setError(await toAppError(caught));
    } finally {
      setBusy(false);
    }
  };
  return { busy, error, run };
}

function ErrorBox({ error }: { error: AppError | null }) {
  return error ? <Alert severity="error" sx={{ mb: 2 }}>{error.message}{Object.values(error.fieldErrors).flat().map((m) => ` · ${m}`)}</Alert> : null;
}

/** Next status (only the transitions the API allows for the current status: shipment.nextStatuses). */
export function ChangeStatusDialog({ open, shipment, onClose, onSubmit }: BaseProps) {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const [status, setStatus] = useState<ShipmentStatus | ''>('');
  const [warehouseId, setWarehouseId] = useState<number | null>(null);
  const [notes, setNotes] = useState('');
  const { busy, error, run } = useSubmit(onSubmit);
  // Delivery / failure / cancellation have their own screens (proof of delivery, reasons).
  const choices = shipment.nextStatuses.filter((s) => !['Delivered', 'DeliveryFailed', 'Cancelled'].includes(s));

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{tx('تغيير الحالة', 'Change status')}</DialogTitle>
      <DialogContent dividers>
        <ErrorBox error={error} />
        <Grid container spacing={2}>
          <Grid size={12}>
            <TextField select label={tx('الحالة الجديدة', 'New status')} value={status} onChange={(e) => setStatus(e.target.value as ShipmentStatus)} required>
              {choices.map((s) => <MenuItem key={s} value={s}>{enumLabel('ShipmentStatus', s, language)}</MenuItem>)}
            </TextField>
          </Grid>
          {status === 'InWarehouse' && (
            <Grid size={12}>
              <LookupSelect source="warehouses" label={tx('المستودع', 'Warehouse')} value={warehouseId} onChange={(v) => setWarehouseId(v as number | null)} params={{ branchId: shipment.currentBranchId }} required />
            </Grid>
          )}
          <Grid size={12}><TextField label={t('common.notes')} multiline minRows={2} value={notes} onChange={(e) => setNotes(e.target.value)} /></Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>{t('common.cancel')}</Button>
        <Button variant="contained" disabled={busy || !status} onClick={() => void run({ status, warehouseId, notes: notes || null })}>{t('common.save')}</Button>
      </DialogActions>
    </Dialog>
  );
}

/** Correction of a wrong status (any status, with a reason; kept as a correction row in the history). */
export function CorrectStatusDialog({ open, shipment, onClose, onSubmit }: BaseProps) {
  const { t } = useTranslation();
  const tx = useTx();
  const language = useLanguage();
  const [status, setStatus] = useState<ShipmentStatus | ''>('');
  const [branchId, setBranchId] = useState<number | null>(shipment.currentBranchId);
  const [reason, setReason] = useState('');
  const { busy, error, run } = useSubmit(onSubmit);
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{tx('تصحيح الحالة', 'Correct status')}</DialogTitle>
      <DialogContent dividers>
        <Alert severity="warning" sx={{ mb: 2 }}>{tx('يُسجّل التصحيح في سجل الحالة وسجل التدقيق.', 'The correction is recorded in the history and the audit log.')}</Alert>
        <ErrorBox error={error} />
        <Grid container spacing={2}>
          <Grid size={6}>
            <TextField select label={tx('الحالة الصحيحة', 'Correct status')} value={status} onChange={(e) => setStatus(e.target.value as ShipmentStatus)} required>
              {enumOptions('ShipmentStatus', language).filter((o) => o.value !== shipment.status).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
            </TextField>
          </Grid>
          <Grid size={6}><LookupSelect source="branches" label={tx('الفرع الحالي', 'Current branch')} value={branchId} onChange={(v) => setBranchId(v as number | null)} /></Grid>
          <Grid size={12}><TextField label={t('common.reason')} required multiline minRows={2} value={reason} onChange={(e) => setReason(e.target.value)} /></Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>{t('common.cancel')}</Button>
        <Button variant="contained" color="warning" disabled={busy || !status || reason.trim().length < 3} onClick={() => void run({ status, reason, currentBranchId: branchId })}>{t('common.save')}</Button>
      </DialogActions>
    </Dialog>
  );
}

export function AssignDriverDialog({ open, shipment, onClose, onSubmit }: BaseProps) {
  const { t } = useTranslation();
  const tx = useTx();
  const [driverId, setDriverId] = useState<number | null>(shipment.assignedDriverId);
  const [vehicleId, setVehicleId] = useState<number | null>(shipment.vehicleId);
  const [notes, setNotes] = useState('');
  const { busy, error, run } = useSubmit(onSubmit);
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{tx('إسناد سائق', 'Assign driver')}</DialogTitle>
      <DialogContent dividers>
        <ErrorBox error={error} />
        <Grid container spacing={2}>
          <Grid size={12}><LookupSelect source="drivers" label={t('common.driver')} value={driverId} onChange={(v, row) => { setDriverId(v as number | null); if (row?.defaultVehicleId) setVehicleId(row.defaultVehicleId as number); }} params={{ branchId: shipment.currentBranchId }} /></Grid>
          <Grid size={12}><LookupSelect source="vehicles" label={tx('المركبة', 'Vehicle')} value={vehicleId} onChange={(v) => setVehicleId(v as number | null)} /></Grid>
          <Grid size={12}><TextField label={t('common.notes')} value={notes} onChange={(e) => setNotes(e.target.value)} /></Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>{t('common.cancel')}</Button>
        <Button variant="contained" disabled={busy} onClick={() => void run({ driverId, vehicleId, notes: notes || null })}>{t('common.save')}</Button>
      </DialogActions>
    </Dialog>
  );
}

/** Invoice from one shipment (draft, or issued at once). */
export function InvoiceFromShipmentDialog({ open, shipment, onClose, onSubmit }: BaseProps) {
  const { t } = useTranslation();
  const tx = useTx();
  const [terms, setTerms] = useState<string>('');
  const [issue, setIssue] = useState(true);
  const { busy, error, run } = useSubmit(onSubmit);
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{tx('فاتورة للشحنة', 'Invoice the shipment')} <span dir="ltr">{shipment.trackingNumber}</span></DialogTitle>
      <DialogContent dividers>
        <ErrorBox error={error} />
        <TextField type="number" label={tx('مدة السداد (أيام)', 'Payment terms (days)')} value={terms} onChange={(e) => setTerms(e.target.value)} sx={{ mb: 2 }} helperText={tx('فارغ = حسب إعدادات العميل', 'Empty = customer default')} />
        <TextField select label={tx('الحالة', 'Status')} value={issue ? 'issue' : 'draft'} onChange={(e) => setIssue(e.target.value === 'issue')}>
          <MenuItem value="issue">{tx('إصدار فوراً', 'Issue now')}</MenuItem>
          <MenuItem value="draft">{tx('مسودة', 'Draft')}</MenuItem>
        </TextField>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>{t('common.cancel')}</Button>
        <Button variant="contained" disabled={busy} onClick={() => void run({ branchId: null, paymentTermsDays: terms === '' ? null : Number(terms), notes: null, issueImmediately: issue })}>{t('common.create')}</Button>
      </DialogActions>
    </Dialog>
  );
}
