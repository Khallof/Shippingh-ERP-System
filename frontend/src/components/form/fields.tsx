import { Checkbox, FormControlLabel, Grid2 as Grid, MenuItem, TextField } from '@mui/material';
import { Controller, type Control, type FieldValues, type Path, type UseFormWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { enumOptions, type EnumName } from '@/i18n/enums';
import { useLanguage } from '@/i18n';
import LookupSelect from './LookupSelect';
import type { LookupName, LookupSource } from './lookups';

export type FieldType =
  | 'text' | 'email' | 'password' | 'textarea' | 'number' | 'money' | 'integer'
  | 'select' | 'multiselect' | 'enum' | 'lookup' | 'checkbox' | 'date' | 'datetime';

export interface FieldDef {
  name: string;
  label: string;
  type?: FieldType;
  required?: boolean;
  /** Grid columns out of 12 (default 6; textarea 12). */
  span?: number;
  options?: { value: string | number; label: string }[];
  enumName?: EnumName;
  lookup?: LookupName | LookupSource;
  /** Extra lookup filters from the other values, e.g. (v) => ({ cityId: v.cityId }). */
  lookupParams?: (values: Record<string, unknown>) => Record<string, unknown>;
  valueKey?: string;
  helperText?: string;
  disabled?: boolean | ((values: Record<string, unknown>) => boolean);
  hidden?: (values: Record<string, unknown>) => boolean;
  min?: number;
  max?: number;
  maxLength?: number;
  /** Only when creating (e.g. a code that cannot change). */
  createOnly?: boolean;
}

interface FieldsProps<T extends FieldValues> {
  fields: FieldDef[];
  control: Control<T>;
  watch: UseFormWatch<T>;
  serverErrors?: Record<string, string[]>;
  editing?: boolean;
}

const toNumber = (text: string) => (text === '' ? null : Number(text));

/**
 * Renders form fields from definitions with react-hook-form. Client rules: required / min / max / maxLength;
 * the API repeats every rule (FluentValidation) and its field errors are shown under the same fields.
 */
export default function FormFields<T extends FieldValues>({ fields, control, watch, serverErrors = {}, editing }: FieldsProps<T>) {
  const { t } = useTranslation();
  const language = useLanguage();
  const values = watch() as Record<string, unknown>;

  return (
    <Grid container spacing={2}>
      {fields
        .filter((field) => !field.hidden?.(values))
        .map((field) => {
          const type = field.type ?? 'text';
          const span = field.span ?? (type === 'textarea' ? 12 : 6);
          const disabled = (typeof field.disabled === 'function' ? field.disabled(values) : field.disabled) || (editing && field.createOnly);
          return (
            <Grid key={field.name} size={{ xs: 12, sm: span }}>
              <Controller
                name={field.name as Path<T>}
                control={control}
                rules={{
                  required: field.required && type !== 'checkbox' ? t('common.required') : undefined,
                  min: field.min !== undefined ? { value: field.min, message: `≥ ${field.min}` } : undefined,
                  max: field.max !== undefined ? { value: field.max, message: `≤ ${field.max}` } : undefined,
                  maxLength: field.maxLength ? { value: field.maxLength, message: `≤ ${field.maxLength}` } : undefined
                }}
                render={({ field: input, fieldState }) => {
                  const serverKey = Object.keys(serverErrors).find((key) => key.toLowerCase() === field.name.toLowerCase());
                  const message = fieldState.error?.message ?? (serverKey ? serverErrors[serverKey][0] : undefined);
                  const common = {
                    label: field.label,
                    required: field.required,
                    disabled,
                    error: !!message,
                    helperText: message ?? field.helperText,
                    name: field.name
                  };

                  switch (type) {
                    case 'checkbox':
                      return (
                        <FormControlLabel
                          control={<Checkbox checked={!!input.value} onChange={(event) => input.onChange(event.target.checked)} disabled={disabled} />}
                          label={field.label}
                        />
                      );
                    case 'lookup':
                      return (
                        <LookupSelect
                          {...common}
                          source={field.lookup!}
                          value={input.value as number | null}
                          valueKey={field.valueKey}
                          params={field.lookupParams?.(values)}
                          onChange={(value) => input.onChange(value)}
                        />
                      );
                    case 'multiselect': {
                      const selected = (input.value as (string | number)[] | null) ?? [];
                      return (
                        <TextField select {...common} value={selected} onChange={(event) => input.onChange(event.target.value as unknown as (string | number)[])}
                          SelectProps={{ multiple: true, renderValue: (value) => (field.options ?? []).filter((o) => (value as (string | number)[]).includes(o.value)).map((o) => o.label).join('، ') }}>
                          {(field.options ?? []).map((option) => (
                            <MenuItem key={String(option.value)} value={option.value}>
                              <Checkbox size="small" checked={selected.includes(option.value)} sx={{ py: 0 }} />
                              {option.label}
                            </MenuItem>
                          ))}
                        </TextField>
                      );
                    }
                    case 'select':
                    case 'enum': {
                      const options = type === 'enum' ? enumOptions(field.enumName!, language) : field.options ?? [];
                      return (
                        <TextField select {...common} value={input.value ?? ''} onChange={(event) => input.onChange(event.target.value === '' ? null : event.target.value)} inputRef={input.ref}>
                          {!field.required && <MenuItem value="">-</MenuItem>}
                          {options.map((option) => (
                            <MenuItem key={String(option.value)} value={option.value}>
                              {option.label}
                            </MenuItem>
                          ))}
                        </TextField>
                      );
                    }
                    case 'number':
                    case 'money':
                    case 'integer':
                      return (
                        <TextField
                          {...common}
                          type="number"
                          value={input.value ?? ''}
                          onChange={(event) => input.onChange(toNumber(event.target.value))}
                          inputRef={input.ref}
                          inputProps={{ step: type === 'integer' ? 1 : type === 'money' ? 0.01 : 'any', min: field.min, max: field.max, dir: 'ltr' }}
                        />
                      );
                    case 'date':
                    case 'datetime':
                      return (
                        <TextField
                          {...common}
                          type={type === 'date' ? 'date' : 'datetime-local'}
                          value={input.value ? String(input.value).slice(0, type === 'date' ? 10 : 16) : ''}
                          onChange={(event) => input.onChange(event.target.value || null)}
                          inputRef={input.ref}
                          InputLabelProps={{ shrink: true }}
                        />
                      );
                    default:
                      return (
                        <TextField
                          {...common}
                          type={type === 'textarea' ? 'text' : type}
                          multiline={type === 'textarea'}
                          minRows={type === 'textarea' ? 2 : undefined}
                          value={input.value ?? ''}
                          onChange={(event) => input.onChange(event.target.value === '' ? null : event.target.value)}
                          inputRef={input.ref}
                          inputProps={{ maxLength: field.maxLength, dir: type === 'email' || type === 'password' ? 'ltr' : undefined }}
                          autoComplete={type === 'password' ? 'new-password' : undefined}
                        />
                      );
                  }
                }}
              />
            </Grid>
          );
        })}
    </Grid>
  );
}
