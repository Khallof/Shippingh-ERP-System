import { useEffect, useMemo, useState } from 'react';
import { Autocomplete, CircularProgress, TextField } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import type { PagedResult } from '@/api/types';
import { useTx } from '@/i18n';
import { lookups, type LookupName, type LookupSource } from './lookups';

interface Option {
  id: number | string;
  label: string;
  row: Record<string, unknown>;
}

interface LookupSelectProps {
  source: LookupName | LookupSource;
  value: number | string | null | undefined;
  onChange(value: number | string | null, row: Record<string, unknown> | null): void;
  label: string;
  required?: boolean;
  disabled?: boolean;
  error?: boolean;
  helperText?: string;
  /** Extra filters (e.g. { cityId } for zones). */
  params?: Record<string, unknown>;
  /** Value field of a row (default "id"; roles use "name"). */
  valueKey?: string;
  name?: string;
}

function useDebounced<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(handle);
  }, [value, delay]);
  return debounced;
}

/**
 * Searchable select over an API list: the server searches (search=...), at most 50 options are loaded. The current
 * value is loaded by id when it is not in the first page (editing an old record).
 */
export default function LookupSelect({ source, value, onChange, label, required, disabled, error, helperText, params, valueKey = 'id', name }: LookupSelectProps) {
  const tx = useTx();
  const lookup: LookupSource = typeof source === 'string' ? lookups[source] : source;
  const [input, setInput] = useState('');
  const [open, setOpen] = useState(false);
  const search = useDebounced(input);
  const toLabel = lookup.label ?? ((row: Record<string, unknown>) => String(row.arabicName ?? row.name ?? row.id));

  const listParams = useMemo(() => ({ ...lookup.params, ...params }), [lookup.params, params]);
  const list = useQuery({
    queryKey: ['lookup', lookup.url, listParams, lookup.unpaged ? '' : search],
    queryFn: async () => {
      if (lookup.unpaged) {
        return api.get<Record<string, unknown>[]>(lookup.url, listParams);
      }

      const page = await api.list<Record<string, unknown>>(lookup.url, { ...(listParams as object), search: search || undefined, pageSize: 50 });
      return (page as PagedResult<Record<string, unknown>>).items;
    },
    enabled: open || value !== null && value !== undefined && value !== '',
    staleTime: 60_000
  });

  const rows = useMemo(() => list.data ?? [], [list.data]);
  const inList = rows.find((row) => String(row[valueKey]) === String(value));
  const single = useQuery({
    queryKey: ['lookup-item', lookup.url, value],
    queryFn: () => api.get<Record<string, unknown>>(`${lookup.url}/${value}`),
    enabled: !lookup.unpaged && valueKey === 'id' && value !== null && value !== undefined && value !== '' && !list.isFetching && !inList,
    staleTime: 300_000
  });

  const options: Option[] = useMemo(() => {
    const result = rows.map((row) => ({ id: row[valueKey] as number | string, label: toLabel(row), row }));
    if (single.data && !result.some((option) => String(option.id) === String(value))) {
      result.unshift({ id: single.data[valueKey] as number | string, label: toLabel(single.data), row: single.data });
    }

    return result;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows, single.data, valueKey, value]);

  const selected = options.find((option) => String(option.id) === String(value)) ?? null;

  return (
    <Autocomplete<Option>
      options={options}
      value={selected}
      open={open}
      onOpen={() => setOpen(true)}
      onClose={() => setOpen(false)}
      disabled={disabled}
      loading={list.isFetching}
      filterOptions={lookup.unpaged ? undefined : (all) => all}
      isOptionEqualToValue={(option, current) => String(option.id) === String(current.id)}
      getOptionLabel={(option) => option.label}
      onInputChange={(_, text, reason) => reason === 'input' && setInput(text)}
      onChange={(_, option) => {
        setInput('');
        onChange(option ? option.id : null, option ? option.row : null);
      }}
      noOptionsText={tx('لا توجد نتائج', 'No results')}
      loadingText={tx('جارٍ البحث…', 'Searching…')}
      renderInput={(inputParams) => (
        <TextField
          {...inputParams}
          name={name}
          label={label}
          required={required}
          error={error}
          helperText={helperText}
          InputProps={{
            ...inputParams.InputProps,
            endAdornment: (
              <>
                {list.isFetching ? <CircularProgress color="inherit" size={16} /> : null}
                {inputParams.InputProps.endAdornment}
              </>
            )
          }}
        />
      )}
    />
  );
}
