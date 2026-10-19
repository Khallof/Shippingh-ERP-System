import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { CacheProvider } from '@emotion/react';
import createCache from '@emotion/cache';
import { prefixer } from 'stylis';
import rtlPlugin from 'stylis-plugin-rtl';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter } from 'react-router-dom';
import { AppError } from '@/api/errors';
import { AuthProvider } from '@/auth/AuthProvider';
import { NotifierProvider } from '@/components/common/Notifier';
import { useLanguage } from '@/i18n';
import { buildTheme } from '@/theme/theme';

// Two emotion caches: the RTL one flips margins / paddings / borders (marginLeft -> marginRight) for Arabic.
const rtlCache = createCache({ key: 'muirtl', stylisPlugins: [prefixer, rtlPlugin] });
const ltrCache = createCache({ key: 'muiltr', stylisPlugins: [prefixer] });

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // 4xx answers will not change on retry; network / 5xx errors get one more try.
      retry: (count, error) => count < 1 && !(error instanceof AppError && error.status >= 400 && error.status < 500)
    },
    mutations: { retry: false }
  }
});

type ColorMode = 'light' | 'dark';
const ColorModeContext = createContext<{ mode: ColorMode; toggle(): void }>({ mode: 'light', toggle: () => undefined });

function storedMode(): ColorMode {
  try {
    return localStorage.getItem('erp.colorMode') === 'dark' ? 'dark' : 'light';
  } catch {
    return 'light';
  }
}

function ThemedApp({ children }: { children: ReactNode }) {
  const language = useLanguage();
  const [mode, setMode] = useState<ColorMode>(storedMode);
  const theme = useMemo(() => buildTheme(language, mode), [language, mode]);
  const colorMode = useMemo(
    () => ({
      mode,
      toggle: () =>
        setMode((current) => {
          const next = current === 'light' ? 'dark' : 'light';
          try {
            localStorage.setItem('erp.colorMode', next);
          } catch {
            // ignore
          }
          return next;
        })
    }),
    [mode]
  );

  return (
    <ColorModeContext.Provider value={colorMode}>
      <CacheProvider value={language === 'ar' ? rtlCache : ltrCache}>
        <ThemeProvider theme={theme}>
          <CssBaseline />
          {children}
        </ThemeProvider>
      </CacheProvider>
    </ColorModeContext.Provider>
  );
}

export function useColorMode() {
  return useContext(ColorModeContext);
}

export default function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemedApp>
        <NotifierProvider>
          <BrowserRouter>
            <AuthProvider>{children}</AuthProvider>
          </BrowserRouter>
        </NotifierProvider>
      </ThemedApp>
    </QueryClientProvider>
  );
}
