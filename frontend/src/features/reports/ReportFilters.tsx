import type { ReactNode } from 'react';
import { Box, Card, Grid2 as Grid, TextField } from '@mui/material';
import { useTranslation } from 'react-i18next';
import LookupSelect from '@/components/form/LookupSelect';
import type { LookupName } from '@/components/form/lookups';

export interface ReportFilterValues {
  [key: string]: string | number | boolean | null | undefined;
}

/** Report filter row: date range, optional lookups and extra controls; reports reload when a value changes. */
export default function ReportFilters({ values, onChange, dates = ['fromDate', 'toDate'], lookups = [], children }: {
  values: ReportFilterValues;
  onChange(values: ReportFilterValues): void;
  dates?: string[];
  lookups?: { name: string; label: string; source: LookupName }[];
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const set = (name: string, value: unknown) => onChange({ ...values, [name]: value === '' ? null : (value as string) });
  return (
    <Card sx={{ p: 1.5, mb: 2 }}>
      <Grid container spacing={1.5} sx={{ alignItems: 'center' }}>
        {dates.map((name) => (
          <Grid key={name} size={{ xs: 6, md: 2 }}>
            <TextField type="date" label={name === 'toDate' ? t('common.toDate') : name === 'asOfDate' ? t('common.date') : t('common.fromDate')} InputLabelProps={{ shrink: true }}
              value={(values[name] as string) ?? ''} onChange={(e) => set(name, e.target.value)} />
          </Grid>
        ))}
        {lookups.map((lookup) => (
          <Grid key={lookup.name} size={{ xs: 12, sm: 6, md: 2.5 }}>
            <LookupSelect source={lookup.source} label={lookup.label} value={(values[lookup.name] as number) ?? null} onChange={(v) => set(lookup.name, v)} />
          </Grid>
        ))}
        <Grid size="grow"><Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end', flexWrap: 'wrap', alignItems: 'center' }}>{children}</Box></Grid>
      </Grid>
    </Card>
  );
}
