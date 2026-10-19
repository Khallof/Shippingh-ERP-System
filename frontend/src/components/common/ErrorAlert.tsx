import { Alert, AlertTitle, Button } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { AppError } from '@/api/errors';

/** Inline error of a query (list, details) with an optional retry button. */
export default function ErrorAlert({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const { t } = useTranslation();
  const appError = error instanceof AppError ? error : null;
  return (
    <Alert
      severity="error"
      sx={{ my: 2 }}
      action={
        onRetry && (
          <Button color="inherit" size="small" onClick={onRetry}>
            {t('common.retry')}
          </Button>
        )
      }
    >
      <AlertTitle>{appError?.message ?? t('errors.unexpected')}</AlertTitle>
      {appError?.traceId && (
        <span>
          {t('common.traceId')}: {appError.traceId}
        </span>
      )}
    </Alert>
  );
}
