import { useEffect, useState, type ReactNode } from 'react';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle } from '@mui/material';
import { useForm, type DefaultValues, type FieldValues } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { AppError, toAppError } from '@/api/errors';
import FormFields, { type FieldDef } from './fields';

interface FormDialogProps<T extends FieldValues> {
  open: boolean;
  title: ReactNode;
  fields: FieldDef[];
  defaultValues: DefaultValues<T>;
  editing?: boolean;
  submitText?: string;
  maxWidth?: 'xs' | 'sm' | 'md' | 'lg';
  /** Returns when saved; throw (AppError) to keep the dialog open with the server errors. */
  onSubmit(values: T): Promise<unknown>;
  onClose(): void;
  children?: ReactNode;
}

/** Create / edit dialog built from field definitions. Server field errors are shown under their fields. */
export default function FormDialog<T extends FieldValues>({ open, title, fields, defaultValues, editing, submitText, maxWidth = 'md', onSubmit, onClose, children }: FormDialogProps<T>) {
  const { t } = useTranslation();
  const form = useForm<T>({ defaultValues });
  const [serverError, setServerError] = useState<AppError | null>(null);

  useEffect(() => {
    if (open) {
      form.reset(defaultValues);
      setServerError(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const submit = form.handleSubmit(async (values) => {
    setServerError(null);
    try {
      await onSubmit(values);
    } catch (error) {
      setServerError(await toAppError(error));
    }
  });

  const busy = form.formState.isSubmitting;
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose} maxWidth={maxWidth} fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>{title}</DialogTitle>
        <DialogContent dividers>
          {serverError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {serverError.message}
              {/* Errors of fields that are not on this form (e.g. paymentDateUtc, allocations[0].amount) are listed here,
                  otherwise the user only sees "invalid data" with nothing to fix. */}
              {Object.entries(serverError.fieldErrors).filter(([key]) => !fields.some((field) => field.name.toLowerCase() === key.toLowerCase())).length > 0 && (
                <ul style={{ margin: '4px 0 0', paddingInlineStart: 18 }}>
                  {Object.entries(serverError.fieldErrors)
                    .filter(([key]) => !fields.some((field) => field.name.toLowerCase() === key.toLowerCase()))
                    .flatMap(([key, messages]) => messages.map((message) => <li key={key + message}>{message}</li>))}
                </ul>
              )}
            </Alert>
          )}
          <FormFields fields={fields} control={form.control} watch={form.watch} serverErrors={serverError?.fieldErrors} editing={editing} />
          {children}
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" variant="contained" disabled={busy}>
            {submitText ?? t('common.save')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
