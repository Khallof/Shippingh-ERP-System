import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api } from '@/api/client';
import type { ListParams } from '@/api/types';

/** A paged list from the API; the previous page stays visible while the next one loads. */
export function useApiList<T>(url: string, params: ListParams | Record<string, unknown>, options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: [url, 'list', params],
    queryFn: () => api.list<T>(url, params as ListParams),
    placeholderData: keepPreviousData,
    enabled: options.enabled ?? true
  });
}
