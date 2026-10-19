import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { Alert, Snackbar } from '@mui/material';
import { AppError } from '@/api/errors';

type Severity = 'success' | 'error' | 'info' | 'warning';

interface NotifyApi {
  success(message: string): void;
  error(error: unknown): void;
  info(message: string): void;
}

const NotifyContext = createContext<NotifyApi | null>(null);

/** One snackbar for the app: success messages and API errors (with the trace id for support). */
export function NotifierProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<{ open: boolean; message: string; severity: Severity; key: number }>({
    open: false,
    message: '',
    severity: 'success',
    key: 0
  });

  const show = useCallback((message: string, severity: Severity) => {
    setState((previous) => ({ open: true, message, severity, key: previous.key + 1 }));
  }, []);

  const api = useMemo<NotifyApi>(
    () => ({
      success: (message) => show(message, 'success'),
      info: (message) => show(message, 'info'),
      error: (error) => {
        if (error instanceof AppError) {
          const fields = Object.values(error.fieldErrors).flat();
          const text = fields.length > 0 ? `${error.message} ${fields.slice(0, 3).join(' · ')}` : error.message;
          show(error.traceId ? `${text} (${error.traceId})` : text, 'error');
        } else {
          show(error instanceof Error ? error.message : String(error), 'error');
        }
      }
    }),
    [show]
  );

  return (
    <NotifyContext.Provider value={api}>
      {children}
      <Snackbar
        key={state.key}
        open={state.open}
        autoHideDuration={state.severity === 'error' ? 9000 : 4000}
        onClose={(_, reason) => reason !== 'clickaway' && setState((previous) => ({ ...previous, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity={state.severity} variant="filled" onClose={() => setState((previous) => ({ ...previous, open: false }))} sx={{ maxWidth: 640 }}>
          {state.message}
        </Alert>
      </Snackbar>
    </NotifyContext.Provider>
  );
}

export function useNotify(): NotifyApi {
  const context = useContext(NotifyContext);
  if (!context) {
    throw new Error('useNotify must be used inside <NotifierProvider>.');
  }

  return context;
}
