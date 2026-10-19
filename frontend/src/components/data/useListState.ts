import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { GridPaginationModel, GridSortModel } from '@mui/x-data-grid';

export type FilterValues = Record<string, string>;

export interface ListState {
  pageNumber: number;
  pageSize: number;
  search: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  filters: FilterValues;
  /** Query parameters for the API (paging + sort + search + filters). */
  params: Record<string, string | number>;
  paginationModel: GridPaginationModel;
  sortModel: GridSortModel;
  setPagination(model: GridPaginationModel): void;
  setSort(model: GridSortModel): void;
  setSearch(search: string): void;
  setFilters(filters: FilterValues): void;
  reset(): void;
}

const RESERVED = new Set(['page', 'size', 'q', 'sort', 'dir']);

/**
 * Paging, sorting, search and filters of a list, kept in the URL (?page=2&size=25&q=...&status=Delivered):
 * a filtered list can be bookmarked, shared and survives a reload / the back button.
 * prefix lets two lists live on one page (e.g. "a_").
 */
export function useListState(options: { defaultPageSize?: number; defaultSort?: string; defaultDirection?: 'asc' | 'desc'; prefix?: string } = {}): ListState {
  const { defaultPageSize = 25, defaultSort, defaultDirection = 'desc', prefix = '' } = options;
  const [searchParams, setSearchParams] = useSearchParams();
  const key = useCallback((name: string) => `${prefix}${name}`, [prefix]);

  const pageNumber = Math.max(1, Number(searchParams.get(key('page')) ?? 1) || 1);
  const pageSize = Math.min(100, Math.max(5, Number(searchParams.get(key('size')) ?? defaultPageSize) || defaultPageSize));
  const search = searchParams.get(key('q')) ?? '';
  const sortBy = searchParams.get(key('sort')) ?? defaultSort;
  const sortDirection = ((searchParams.get(key('dir')) as 'asc' | 'desc' | null) ?? (sortBy ? defaultDirection : undefined)) || undefined;

  const filtersKey = searchParams.toString();
  const filters = useMemo(() => {
    const values: FilterValues = {};
    new URLSearchParams(filtersKey).forEach((value, name) => {
      if (name.startsWith(prefix) && !RESERVED.has(name.slice(prefix.length)) && value !== '') {
        values[name.slice(prefix.length)] = value;
      }
    });
    return values;
  }, [filtersKey, prefix]);

  const update = useCallback(
    (changes: Record<string, string | number | undefined>, resetPage = true) => {
      const build = (previous: URLSearchParams) => {
      const next = new URLSearchParams(previous);
      Object.entries(changes).forEach(([name, value]) => {
        if (value === undefined || value === '') {
          next.delete(key(name));
        } else {
          next.set(key(name), String(value));
        }
      });
      if (resetPage) {
        next.delete(key('page'));
      }

      return next;
      };

      // Same URL: do not navigate at all (the grid re-reports its models on every render).
      const current = new URLSearchParams(window.location.search);
      if (build(current).toString() !== current.toString()) {
        setSearchParams(build, { replace: true });
      }
    },
    [key, setSearchParams]
  );

  const params = useMemo(() => {
    const result: Record<string, string | number> = { pageNumber, pageSize, ...filters };
    if (search) result.search = search;
    if (sortBy) result.sortBy = sortBy;
    if (sortDirection) result.sortDirection = sortDirection;
    return result;
  }, [pageNumber, pageSize, filters, search, sortBy, sortDirection]);

  const paginationModel = useMemo(() => ({ page: pageNumber - 1, pageSize }), [pageNumber, pageSize]);
  const sortModel = useMemo<GridSortModel>(() => (sortBy ? [{ field: sortBy, sort: sortDirection ?? 'desc' }] : []), [sortBy, sortDirection]);

  return {
    pageNumber,
    pageSize,
    search,
    sortBy,
    sortDirection,
    filters,
    params,
    paginationModel,
    sortModel,
    setPagination: (model) => model.page !== paginationModel.page || model.pageSize !== paginationModel.pageSize ? update({ page: model.page + 1 === 1 ? undefined : model.page + 1, size: model.pageSize === defaultPageSize ? undefined : model.pageSize }, false) : undefined,
    setSort: (model) => (model[0]?.field !== sortModel[0]?.field || model[0]?.sort !== sortModel[0]?.sort ? update({ sort: model[0]?.field, dir: model[0]?.sort ?? undefined }) : undefined),
    setSearch: (value) => update({ q: value.trim() || undefined }),
    setFilters: (values) => {
      const cleared: Record<string, string | undefined> = {};
      Object.keys(filters).forEach((name) => (cleared[name] = undefined));
      update({ ...cleared, ...values });
    },
    reset: () => {
      const cleared: Record<string, undefined> = { q: undefined, sort: undefined, dir: undefined, size: undefined };
      Object.keys(filters).forEach((name) => (cleared[name] = undefined));
      update(cleared);
    }
  };
}
