import { useMemo, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { DataGrid, type GridColDef, type GridCellParams, type GridValidRowModel } from '@mui/x-data-grid';
import { Box, Card } from '@mui/material';
import { useTranslation } from 'react-i18next';
import type { PagedResult } from '@/api/types';
import ErrorAlert from '@/components/common/ErrorAlert';
import type { ListState } from './useListState';

interface DataTableProps<T extends GridValidRowModel> {
  columns: GridColDef<T>[];
  data: PagedResult<T> | undefined;
  loading: boolean;
  error?: unknown;
  onRetry?: () => void;
  state: ListState;
  /** Row click opens this path (e.g. id => `/shipments/${id}`). */
  rowLink?: (row: T) => string;
  /** Row click handler (instead of rowLink), e.g. open a details dialog. */
  onRowClick?: (row: T) => void;
  getRowId?: (row: T) => string | number;
  height?: number | string;
  checkboxSelection?: boolean;
  onSelectionChange?: (ids: (string | number)[]) => void;
}

/**
 * Server-side table: the API pages, sorts and filters (pageNumber / pageSize / sortBy / sortDirection).
 * The column "field" is sent as sortBy: mark a column sortable: true only when the API allows that sort name.
 */
export default function DataTable<T extends GridValidRowModel>({
  columns,
  data,
  loading,
  error,
  onRetry,
  state,
  rowLink,
  onRowClick,
  getRowId,
  height = 'calc(100vh - 270px)',
  checkboxSelection,
  onSelectionChange
}: DataTableProps<T>) {
  const navigate = useNavigate();
  const { t } = useTranslation();

  // The API rejects unknown sortBy values (400): only columns marked sortable: true can be sorted.
  // Report a selection only when it really changed: the grid also fires the event when columns / rows are replaced,
  // and a parent that stores the selection in state would otherwise re-render in a loop.
  const lastSelection = useRef('');
  const handleSelection = (ids: (string | number)[]) => {
    const key = ids.map(String).sort().join(',');
    if (key !== lastSelection.current) {
      lastSelection.current = key;
      onSelectionChange?.(ids);
    }
  };

  const safeColumns = useMemo(() => columns.map((column) => ({ ...column, sortable: column.sortable === true })), [columns]);

  // The grid drops (and reports) a sort on a column it does not show; the default API sort of a list may be such a
  // column (e.g. createdAtUtc), so only pass sorts on visible sortable columns.
  const sortModel = useMemo(
    () => state.sortModel.filter((item) => safeColumns.some((column) => column.field === item.field && column.sortable)),
    [state.sortModel, safeColumns]
  );

  if (error && !data) {
    return <ErrorAlert error={error} onRetry={onRetry} />;
  }

  return (
    <Card>
      <Box sx={{ height, minHeight: 360, width: '100%' }}>
        <DataGrid<T>
          rows={data?.items ?? []}
          columns={safeColumns}
          getRowId={getRowId}
          loading={loading}
          rowCount={data?.totalCount ?? 0}
          paginationMode="server"
          sortingMode="server"
          filterMode="server"
          paginationModel={state.paginationModel}
          onPaginationModelChange={state.setPagination}
          sortModel={sortModel}
          onSortModelChange={(model) => (model.length > 0 || sortModel.length > 0 ? state.setSort(model) : undefined)}
          pageSizeOptions={[10, 25, 50, 100]}
          disableColumnFilter
          disableRowSelectionOnClick={!checkboxSelection}
          checkboxSelection={checkboxSelection}
          keepNonExistentRowsSelected={checkboxSelection}
          onRowSelectionModelChange={onSelectionChange ? (model) => handleSelection([...model]) : undefined}
          onCellClick={rowLink || onRowClick ? (params: GridCellParams<T>) => {
            if (params.field === '__check__' || params.field === '__actions') return;
            if (onRowClick) onRowClick(params.row);
            else navigate(rowLink!(params.row));
          } : undefined}
          localeText={{ noRowsLabel: t('common.noData') }}
          sx={{ '& .MuiDataGrid-row': { cursor: rowLink || onRowClick ? 'pointer' : 'default' } }}
        />
      </Box>
    </Card>
  );
}
