import { useEffect, useState } from 'react';
import { Box, Button, Card, Grid2 as Grid, InputAdornment, MenuItem, TextField } from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { useTranslation } from 'react-i18next';
import { enumOptions, type EnumName } from '@/i18n/enums';
import { useLanguage } from '@/i18n';
import LookupSelect from '@/components/form/LookupSelect';
import type { LookupName } from '@/components/form/lookups';
import type { ListState } from './useListState';

export interface FilterDef {
  name: string;
  label: string;
  type: 'enum' | 'select' | 'lookup' | 'date' | 'boolean' | 'text';
  enumName?: EnumName;
  options?: { value: string; label: string }[];
  lookup?: LookupName;
  /** Grid columns out of 12 (default 2). */
  span?: number;
}

/**
 * Search box + filters in one row above the table. Applies on Enter / the search button (selects apply at once).
 */
export default function FilterBar({ state, filters = [], searchPlaceholder, children }: { state: ListState; filters?: FilterDef[]; searchPlaceholder?: string; children?: React.ReactNode }) {
  const { t } = useTranslation();
  const language = useLanguage();
  const [search, setSearch] = useState(state.search);
  const [draft, setDraft] = useState(state.filters);

  useEffect(() => setSearch(state.search), [state.search]);
  useEffect(() => setDraft(state.filters), [state.filters]);

  const apply = (next = draft) => {
    state.setFilters(next);
    if (search !== state.search) {
      state.setSearch(search);
    }
  };

  const set = (name: string, value: string | null | undefined, immediate = true) => {
    const next = { ...draft };
    if (value === null || value === undefined || value === '') {
      delete next[name];
    } else {
      next[name] = value;
    }

    setDraft(next);
    if (immediate) {
      state.setFilters(next);
    }
  };

  const yesNo = [
    { value: 'true', label: t('common.yes') },
    { value: 'false', label: t('common.no') }
  ];

  return (
    <Card sx={{ p: 1.5, mb: 2 }}>
      <Grid container spacing={1.5} sx={{ alignItems: 'center' }}>
        <Grid size={{ xs: 12, md: 3 }}>
          <TextField
            value={search}
            placeholder={searchPlaceholder ?? t('common.search')}
            onChange={(event) => setSearch(event.target.value)}
            onKeyDown={(event) => event.key === 'Enter' && apply()}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" />
                </InputAdornment>
              )
            }}
          />
        </Grid>
        {filters.map((filter) => (
          <Grid key={filter.name} size={{ xs: 12, sm: 6, md: filter.span ?? 2 }}>
            {filter.type === 'lookup' ? (
              <LookupSelect
                source={filter.lookup!}
                label={filter.label}
                value={draft[filter.name] ? Number(draft[filter.name]) : null}
                onChange={(value) => set(filter.name, value === null ? null : String(value))}
              />
            ) : filter.type === 'date' || filter.type === 'text' ? (
              <TextField
                type={filter.type === 'date' ? 'date' : 'text'}
                label={filter.label}
                value={draft[filter.name] ?? ''}
                InputLabelProps={{ shrink: filter.type === 'date' ? true : undefined }}
                onChange={(event) => set(filter.name, event.target.value, filter.type === 'date')}
                onKeyDown={(event) => event.key === 'Enter' && apply()}
              />
            ) : (
              <TextField select label={filter.label} value={draft[filter.name] ?? ''} onChange={(event) => set(filter.name, event.target.value)}>
                <MenuItem value="">{t('common.all')}</MenuItem>
                {(filter.type === 'enum' ? enumOptions(filter.enumName!, language) : filter.type === 'boolean' ? yesNo : filter.options ?? []).map((option) => (
                  <MenuItem key={option.value} value={option.value}>
                    {option.label}
                  </MenuItem>
                ))}
              </TextField>
            )}
          </Grid>
        ))}
        <Grid size="grow">
          <Box sx={{ display: 'flex', gap: 1, justifyContent: 'flex-end', flexWrap: 'wrap' }}>
            <Button variant="contained" onClick={() => apply()} startIcon={<SearchIcon />}>
              {t('common.search')}
            </Button>
            <Button onClick={() => { setSearch(''); state.reset(); }}>{t('common.reset')}</Button>
            {children}
          </Box>
        </Grid>
      </Grid>
    </Card>
  );
}
