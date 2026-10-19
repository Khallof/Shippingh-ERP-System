import { createTheme, type Theme } from '@mui/material/styles';
import { arSD, enUS } from '@mui/material/locale';
import { arSD as gridArSD, enUS as gridEnUS } from '@mui/x-data-grid/locales';
import type {} from '@mui/x-data-grid/themeAugmentation';
import { surfaces } from './palette';

/**
 * Design system: MUI with direction rtl (Arabic) or ltr (English), the Cairo font (Arabic + Latin, bundled by
 * @fontsource, no CDN), compact density for data entry, 8 px radius, recessive borders.
 */
export function buildTheme(language: 'ar' | 'en', mode: 'light' | 'dark'): Theme {
  const isDark = mode === 'dark';
  return createTheme(
    {
      direction: language === 'ar' ? 'rtl' : 'ltr',
      palette: {
        mode,
        primary: { main: isDark ? '#5b9cf0' : '#1d5fb8' },
        secondary: { main: isDark ? '#e08a5c' : '#c4561f' },
        success: { main: '#0ca30c' },
        warning: { main: '#c98500' },
        error: { main: '#d03b3b' },
        background: { default: isDark ? '#121211' : '#f3f4f6', paper: isDark ? surfaces.dark : '#ffffff' }
      },
      shape: { borderRadius: 8 },
      typography: {
        fontFamily: '"Cairo", "Segoe UI", Tahoma, sans-serif',
        fontSize: 14,
        h4: { fontWeight: 700, fontSize: '1.6rem' },
        h5: { fontWeight: 700, fontSize: '1.3rem' },
        h6: { fontWeight: 700, fontSize: '1.1rem' },
        button: { fontWeight: 600, textTransform: 'none' }
      },
      components: {
        MuiButton: { defaultProps: { disableElevation: true } },
        MuiTextField: { defaultProps: { size: 'small', fullWidth: true } },
        MuiFormControl: { defaultProps: { size: 'small' } },
        MuiAutocomplete: { defaultProps: { size: 'small' } },
        MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { backgroundImage: 'none' } } },
        MuiCard: { defaultProps: { variant: 'outlined' } },
        MuiChip: { defaultProps: { size: 'small' } },
        MuiTooltip: { defaultProps: { arrow: true } },
        MuiDataGrid: {
          defaultProps: { density: 'compact' },
          styleOverrides: { root: { border: 'none' }, columnHeaders: { fontWeight: 700 } }
        }
      }
    },
    language === 'ar' ? arSD : enUS,
    language === 'ar' ? gridArSD : gridEnUS
  );
}
