// Envelope and paging types of the API (hand written; everything else is in generated.ts).

/** Error part of the standard envelope. details: field name -> messages (400 VALIDATION_ERROR). */
export interface ApiErrorBody {
  code: string;
  message: string;
  details?: Record<string, string[]> | null;
}

/** Every JSON response of the API: { success, message, data, error, traceId, timestampUtc }. */
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
  error: ApiErrorBody | null;
  traceId: string | null;
  timestampUtc: string;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/** Common list parameters (PagedQuery on the server). */
export interface ListParams {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  [filter: string]: string | number | boolean | null | undefined;
}

export type ExportFormat = 'excel' | 'pdf';
