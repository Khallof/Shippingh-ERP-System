import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Button, IconButton, Tooltip } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditOutlinedIcon from '@mui/icons-material/EditOutlined';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import ToggleOnOutlinedIcon from '@mui/icons-material/ToggleOnOutlined';
import ToggleOffOutlinedIcon from '@mui/icons-material/ToggleOffOutlined';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { GridColDef, GridValidRowModel } from '@mui/x-data-grid';
import { useTranslation } from 'react-i18next';
import { api, idempotent, newIdempotencyKey } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import PageHeader from '@/components/common/PageHeader';
import ExportMenu from '@/components/common/ExportMenu';
import { ConfirmDialog } from '@/components/common/Dialogs';
import { useNotify } from '@/components/common/Notifier';
import FormDialog from '@/components/form/FormDialog';
import type { FieldDef } from '@/components/form/fields';
import DataTable from './DataTable';
import FilterBar, { type FilterDef } from './FilterBar';
import { useApiList } from './useApiList';
import { useListState } from './useListState';

export interface ResourceConfig<T extends GridValidRowModel> {
  title: string;
  subtitle?: string;
  /** API collection, e.g. "/branches" (GET list, POST, PUT {id}, PATCH {id}/activate|deactivate, DELETE {id}). */
  url: string;
  columns: GridColDef<T>[];
  filters?: FilterDef[];
  searchPlaceholder?: string;
  defaultSort?: string;
  defaultDirection?: 'asc' | 'desc';
  /** Create / edit form. Without it the page is read-only. */
  form?: {
    fields: FieldDef[];
    /** Values of the form for a new row or an existing one (default: the DTO properties named like the fields). */
    toValues?: (row?: T) => Record<string, unknown>;
    toRequest?: (values: Record<string, unknown>, row?: T) => unknown | Promise<unknown>;
    maxWidth?: 'xs' | 'sm' | 'md' | 'lg';
    canEdit?: (row: T) => boolean;
    /** Sends an Idempotency-Key (created when the dialog opens) with the POST. */
    idempotent?: boolean;
  };
  /** Permission for create / edit / activate / delete (the API checks it again). */
  managePermission?: string;
  canActivate?: boolean;
  canDelete?: boolean | ((row: T) => boolean);
  rowLink?: (row: T) => string;
  exportUrl?: string;
  /** POST target when it differs from url (e.g. /invoices/consolidated). */
  createUrl?: string;
  /** Called with the created document (e.g. open it). */
  onCreated?: (created: unknown) => void;
  createLabel?: string;
  rowActions?: (row: T) => ReactNode;
  headerActions?: ReactNode;
  /** Fixed filters (e.g. paymentType=Expense) sent with every request. */
  fixedParams?: Record<string, string | number | boolean>;
  /** Used when two lists share a page (URL keys get this prefix). */
  prefix?: string;
  hideHeader?: boolean;
  /** Opens the create dialog when the page loads (e.g. "record payment" from an invoice). */
  autoOpenCreate?: boolean;
  height?: number | string;
}

/**
 * Generic list + create / edit / activate / delete screen of a resource. Master data, pricing plans, cashboxes and
 * most simple screens are configurations of this component.
 */
export default function ResourcePage<T extends GridValidRowModel & { id: number }>({ config }: { config: ResourceConfig<T> }) {
  const { t } = useTranslation();
  const notify = useNotify();
  const { hasPermission } = useAuth();
  const queryClient = useQueryClient();
  const state = useListState({ defaultSort: config.defaultSort, defaultDirection: config.defaultDirection, prefix: config.prefix });
  const params = useMemo(() => ({ ...config.fixedParams, ...state.params }), [config.fixedParams, state.params]);
  const list = useApiList<T>(config.url, params);
  const canManage = !config.managePermission || hasPermission(config.managePermission);

  const [editing, setEditingState] = useState<{ row?: T; key?: string } | null>(null);
  const setEditing = (value: { row?: T } | null) => setEditingState(value ? { ...value, key: newIdempotencyKey() } : null);
  const [deleting, setDeleting] = useState<T | null>(null);

  useEffect(() => {
    if (config.autoOpenCreate) setEditing({});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [config.url] });

  const toggle = useMutation({
    mutationFn: (row: T) => api.patch(`${config.url}/${row.id}/${(row as { isActive?: boolean }).isActive ? 'deactivate' : 'activate'}`),
    onSuccess: () => {
      notify.success(t('common.done'));
      void invalidate();
    },
    onError: notify.error
  });

  const remove = useMutation({
    mutationFn: (row: T) => api.delete(`${config.url}/${row.id}`),
    onSuccess: () => {
      notify.success(t('common.done'));
      setDeleting(null);
      void invalidate();
    },
    onError: notify.error
  });

  const toValues = (row?: T): Record<string, unknown> => {
    if (config.form?.toValues) {
      return config.form.toValues(row);
    }

    const values: Record<string, unknown> = {};
    config.form?.fields.forEach((field) => {
      values[field.name] = row ? (row as Record<string, unknown>)[field.name] ?? null : field.type === 'checkbox' ? false : null;
    });
    return values;
  };

  const columns = useMemo<GridColDef<T>[]>(() => {
    const hasActions = canManage && (config.form || config.canActivate || config.canDelete);
    if (!hasActions && !config.rowActions) {
      return config.columns;
    }

    return [
      ...config.columns,
      {
        field: '__actions',
        headerName: t('common.actions'),
        sortable: false,
        width: 150,
        renderCell: ({ row }) => (
          <span onClick={(event) => event.stopPropagation()}>
            {config.rowActions?.(row)}
            {canManage && config.form && (config.form.canEdit?.(row) ?? true) && (
              <Tooltip title={t('common.edit')}>
                <IconButton size="small" onClick={() => setEditing({ row })}>
                  <EditOutlinedIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
            {canManage && config.canActivate && (
              <Tooltip title={(row as { isActive?: boolean }).isActive ? t('common.deactivate') : t('common.activate')}>
                <IconButton size="small" onClick={() => toggle.mutate(row)} disabled={toggle.isPending}>
                  {(row as { isActive?: boolean }).isActive ? <ToggleOnOutlinedIcon fontSize="small" color="success" /> : <ToggleOffOutlinedIcon fontSize="small" />}
                </IconButton>
              </Tooltip>
            )}
            {canManage && (typeof config.canDelete === 'function' ? config.canDelete(row) : config.canDelete) && (
              <Tooltip title={t('common.delete')}>
                <IconButton size="small" color="error" onClick={() => setDeleting(row)}>
                  <DeleteOutlineIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            )}
          </span>
        )
      }
    ];
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [config, canManage, t]);

  return (
    <>
      {!config.hideHeader && (
        <PageHeader
          title={config.title}
          subtitle={config.subtitle}
          actions={
            <>
              {config.headerActions}
              {config.exportUrl && <ExportMenu url={config.exportUrl} params={params} />}
              {canManage && config.form && (
                <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing({})}>
                  {config.createLabel ?? t('common.add')}
                </Button>
              )}
            </>
          }
        />
      )}
      {config.hideHeader && canManage && config.form && (
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing({})} sx={{ mb: 1 }}>
          {config.createLabel ?? t('common.add')}
        </Button>
      )}
      <FilterBar state={state} filters={config.filters} searchPlaceholder={config.searchPlaceholder} />
      <DataTable<T>
        columns={columns}
        data={list.data}
        loading={list.isFetching}
        error={list.error}
        onRetry={() => void list.refetch()}
        state={state}
        rowLink={config.rowLink}
        height={config.height}
      />

      {config.form && (
        <FormDialog<Record<string, unknown>>
          open={!!editing}
          title={editing?.row ? `${t('common.edit')} - ${config.title}` : config.createLabel ?? `${t('common.add')} - ${config.title}`}
          fields={config.form.fields}
          defaultValues={toValues(editing?.row)}
          editing={!!editing?.row}
          maxWidth={config.form.maxWidth}
          onClose={() => setEditing(null)}
          onSubmit={async (values) => {
            const body = config.form!.toRequest ? await config.form!.toRequest(values, editing?.row) : values;
            if (editing?.row) {
              await api.put(`${config.url}/${editing.row.id}`, body);
            } else {
              const created = await api.post(config.createUrl ?? config.url, body, config.form!.idempotent && editing?.key ? idempotent(editing.key) : undefined);
              config.onCreated?.(created);
            }

            notify.success(t('common.saved'));
            setEditing(null);
            await invalidate();
          }}
        />
      )}

      <ConfirmDialog
        open={!!deleting}
        danger
        title={t('common.delete')}
        message={t('common.confirm') + '?'}
        busy={remove.isPending}
        onConfirm={() => deleting && remove.mutate(deleting)}
        onClose={() => setDeleting(null)}
      />
    </>
  );
}
