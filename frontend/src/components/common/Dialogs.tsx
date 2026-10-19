import { useState, type ReactNode } from 'react';
import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';

interface ConfirmDialogProps {
  open: boolean;
  title: ReactNode;
  message?: ReactNode;
  confirmText?: string;
  danger?: boolean;
  busy?: boolean;
  onConfirm(): void;
  onClose(): void;
}

export function ConfirmDialog({ open, title, message, confirmText, danger, busy, onConfirm, onClose }: ConfirmDialogProps) {
  const { t } = useTranslation();
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      {message && (
        <DialogContent>
          <DialogContentText>{message}</DialogContentText>
        </DialogContent>
      )}
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          {t('common.cancel')}
        </Button>
        <Button variant="contained" color={danger ? 'error' : 'primary'} onClick={onConfirm} disabled={busy}>
          {confirmText ?? t('common.confirm')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

interface ReasonDialogProps {
  open: boolean;
  title: ReactNode;
  label?: string;
  required?: boolean;
  busy?: boolean;
  confirmText?: string;
  danger?: boolean;
  onSubmit(reason: string): void;
  onClose(): void;
}

/** Asks for a reason (cancel, reject, reverse): the API keeps it in the history and the audit log. */
export function ReasonDialog({ open, title, label, required = true, busy, confirmText, danger, onSubmit, onClose }: ReasonDialogProps) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const invalid = required && reason.trim().length < 3;

  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth="sm" fullWidth TransitionProps={{ onExited: () => setReason('') }}>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <TextField
          autoFocus
          multiline
          minRows={3}
          sx={{ mt: 1 }}
          label={label ?? t('common.reason')}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          inputProps={{ maxLength: 500 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>
          {t('common.cancel')}
        </Button>
        <Button variant="contained" color={danger ? 'error' : 'primary'} disabled={invalid || busy} onClick={() => onSubmit(reason.trim())}>
          {confirmText ?? t('common.confirm')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
