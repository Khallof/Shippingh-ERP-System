import type { GridColDef, GridValidRowModel } from '@mui/x-data-grid';
import StatusChip, { ActiveChip } from '@/components/common/StatusChip';
import { enumLabel, type EnumName } from '@/i18n/enums';
import { formatDate, formatDateTime, formatMoney, formatNumber } from '@/utils/format';

type Col<T extends GridValidRowModel> = GridColDef<T>;
type Extra<T extends GridValidRowModel> = Partial<Col<T>>;

/** Marks a column sortable (only for the API's allowed sort fields). */
export const S = { sortable: true } as const;

/**
 * Column helpers. "field" is the DTO property (camelCase) and also the sortBy sent to the API.
 * Pass sortable: false for columns the API cannot sort.
 */
export const col = {
  text: <T extends GridValidRowModel>(field: string, headerName: string, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    flex: 1,
    minWidth: 120,
    ...extra
  }),

  number: <T extends GridValidRowModel>(field: string, headerName: string, digits = 0, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    type: 'number',
    width: 110,
    headerAlign: 'left',
    align: 'left',
    valueFormatter: (value: number | null) => formatNumber(value, digits),
    ...extra
  }),

  money: <T extends GridValidRowModel>(field: string, headerName: string, currencyField?: string, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    type: 'number',
    width: 140,
    headerAlign: 'left',
    align: 'left',
    renderCell: ({ row, value }) => (
      <span style={{ fontVariantNumeric: 'tabular-nums', direction: 'ltr', unicodeBidi: 'isolate' }}>
        {formatMoney(value as number, currencyField ? (row as Record<string, string>)[currencyField] : undefined)}
      </span>
    ),
    ...extra
  }),

  date: <T extends GridValidRowModel>(field: string, headerName: string, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    width: 115,
    valueFormatter: (value: string | null) => formatDate(value),
    ...extra
  }),

  dateTime: <T extends GridValidRowModel>(field: string, headerName: string, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    width: 150,
    valueFormatter: (value: string | null) => formatDateTime(value),
    ...extra
  }),

  status: <T extends GridValidRowModel>(field: string, headerName: string, enumName: EnumName, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    width: 160,
    renderCell: ({ value }) => <StatusChip enumName={enumName} value={value as string} />,
    ...extra
  }),

  enumText: <T extends GridValidRowModel>(field: string, headerName: string, enumName: EnumName, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    width: 140,
    valueFormatter: (value: string | null) => enumLabel(enumName, value, document.documentElement.lang === 'en' ? 'en' : 'ar'),
    ...extra
  }),

  active: <T extends GridValidRowModel>(field = 'isActive', headerName = 'الحالة', extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    width: 110,
    renderCell: ({ value }) => <ActiveChip active={!!value} />,
    ...extra
  }),

  bool: <T extends GridValidRowModel>(field: string, headerName: string, extra: Extra<T> = {}): Col<T> => ({
    field,
    headerName,
    width: 90,
    type: 'boolean',
    ...extra
  })
};
