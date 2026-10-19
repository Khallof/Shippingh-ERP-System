import { useRef, useState } from 'react';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Grid2 as Grid, TextField, Typography } from '@mui/material';
import PhotoCameraOutlinedIcon from '@mui/icons-material/PhotoCameraOutlined';
import { useTranslation } from 'react-i18next';
import { AppError, toAppError } from '@/api/errors';
import { shipmentsApi } from '@/api/endpoints/shipments';
import type { ProofOfDeliveryDto } from '@/api/generated';
import SignaturePad, { type SignaturePadHandle } from '@/components/form/SignaturePad';
import { useTx } from '@/i18n';
import { formatMoney } from '@/utils/format';

interface DeliverDialogProps {
  open: boolean;
  shipmentId: number;
  trackingNumber: string;
  defaultRecipient?: string;
  codAmount?: number;
  /** Step 9: shipping charges collected from the recipient (charges on the recipient). */
  chargesAmount?: number;
  currencyCode?: string;
  onClose(): void;
  onDelivered(proof: ProofOfDeliveryDto): void;
}

/**
 * Proof of delivery: recipient name / relation, signature (canvas -> PNG), optional photo (camera on phones) and the
 * device location when the user allows it. Sent as multipart to POST /shipments/{id}/deliver.
 */
export default function DeliverDialog({ open, shipmentId, trackingNumber, defaultRecipient, codAmount, chargesAmount, currencyCode, onClose, onDelivered }: DeliverDialogProps) {
  const { t } = useTranslation();
  const tx = useTx();
  const signature = useRef<SignaturePadHandle>(null);
  const [recipientName, setRecipientName] = useState(defaultRecipient ?? '');
  const [relation, setRelation] = useState('');
  const [notes, setNotes] = useState('');
  const [photo, setPhoto] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<AppError | null>(null);

  const location = (): Promise<GeolocationPosition | null> =>
    new Promise((resolve) => {
      if (!navigator.geolocation) return resolve(null);
      navigator.geolocation.getCurrentPosition(resolve, () => resolve(null), { timeout: 5000, maximumAge: 60_000 });
    });

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const [blob, position] = await Promise.all([signature.current?.toBlob() ?? null, location()]);
      const proof = await shipmentsApi.deliver(
        shipmentId,
        {
          recipientName: recipientName.trim(),
          recipientRelation: relation.trim() || null,
          notes: notes.trim() || null,
          latitude: position ? Number(position.coords.latitude.toFixed(6)) : null,
          longitude: position ? Number(position.coords.longitude.toFixed(6)) : null
        },
        blob,
        photo
      );
      onDelivered(proof);
    } catch (caught) {
      setError(await toAppError(caught));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{tx('تأكيد التسليم', 'Confirm delivery')} · <span dir="ltr">{trackingNumber}</span></DialogTitle>
      <DialogContent dividers>
        {!!codAmount && codAmount > 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {tx('حصّل من المستلم', 'Collect from the recipient')}: <strong>{formatMoney(codAmount, currencyCode)}</strong>
          </Alert>
        )}
        {!!chargesAmount && chargesAmount > 0 && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            {tx('أجور الشحن على المستلم', 'Shipping charges on the recipient')}: <strong>{formatMoney(chargesAmount, currencyCode)}</strong>
            <br />
            {tx('تُسجَّل في الفرع بفاتورة باسم المستلم ثم سند قبض.', 'Recorded at the branch as an invoice to the recipient, then a receipt.')}
          </Alert>
        )}
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error.message}{Object.values(error.fieldErrors).flat().map((m) => ` · ${m}`)}</Alert>}
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 7 }}>
            <TextField label={tx('اسم المستلم', 'Recipient name')} required value={recipientName} onChange={(e) => setRecipientName(e.target.value)} inputProps={{ maxLength: 200 }} />
          </Grid>
          <Grid size={{ xs: 12, sm: 5 }}>
            <TextField label={tx('صلة القرابة', 'Relation')} value={relation} onChange={(e) => setRelation(e.target.value)} placeholder={tx('نفسه، زوجة، حارس…', 'Self, spouse, guard…')} />
          </Grid>
          <Grid size={12}>
            <SignaturePad ref={signature} />
          </Grid>
          <Grid size={12}>
            <Button component="label" variant="outlined" startIcon={<PhotoCameraOutlinedIcon />}>
              {photo ? photo.name : tx('صورة التسليم (اختياري)', 'Delivery photo (optional)')}
              <input hidden type="file" accept="image/jpeg,image/png,image/webp" capture="environment" onChange={(e) => setPhoto(e.target.files?.[0] ?? null)} />
            </Button>
            <Typography variant="caption" color="text.secondary" display="block">{tx('JPEG / PNG / WEBP حتى 5 ميغابايت', 'JPEG / PNG / WEBP up to 5 MB')}</Typography>
          </Grid>
          <Grid size={12}>
            <TextField label={t('common.notes')} multiline minRows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>{t('common.cancel')}</Button>
        <Button variant="contained" color="success" onClick={() => void submit()} disabled={busy || recipientName.trim().length < 2}>
          {tx('تأكيد التسليم', 'Confirm delivery')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
