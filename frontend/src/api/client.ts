import type { AxiosRequestConfig } from 'axios';
import { toAppError } from './errors';
import { http } from './http';
import type { ApiResponse, ExportFormat, ListParams, PagedResult } from './types';

async function unwrap<T>(request: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  try {
    const response = await request;
    return response.data.data as T;
  } catch (error) {
    throw await toAppError(error);
  }
}

export interface DownloadedFile {
  blob: Blob;
  fileName: string;
}

function fileNameFrom(disposition: string | undefined, fallback: string): string {
  if (!disposition) {
    return fallback;
  }

  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (utf8) {
    return decodeURIComponent(utf8[1]);
  }

  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1] : fallback;
}

/** The API returns links such as "/api/v1/shipments/5/attachments/9/content"; axios already adds the base URL. */
export function apiPath(url: string): string {
  return url.replace(/^\/api\/v\d+/i, '');
}

/**
 * Idempotency-Key for money-moving POSTs (payments, vouchers, journal entries, COD handovers): created once per
 * dialog, so a double click or a network retry returns the first document instead of posting twice.
 */
export function newIdempotencyKey(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export const idempotent = (key: string) => ({ headers: { 'Idempotency-Key': key } });

/** Thin typed wrappers: they return the "data" of the envelope and throw AppError. */
export const api = {
  get: <T>(url: string, params?: object, config?: AxiosRequestConfig) =>
    unwrap<T>(http.get<ApiResponse<T>>(url, { ...config, params })),

  list: <T>(url: string, params?: ListParams) => unwrap<PagedResult<T>>(http.get<ApiResponse<PagedResult<T>>>(url, { params })),

  post: <T>(url: string, body?: unknown, config?: AxiosRequestConfig) =>
    unwrap<T>(http.post<ApiResponse<T>>(url, body ?? {}, config)),

  put: <T>(url: string, body?: unknown, config?: AxiosRequestConfig) =>
    unwrap<T>(http.put<ApiResponse<T>>(url, body ?? {}, config)),

  patch: <T>(url: string, body?: unknown, config?: AxiosRequestConfig) =>
    unwrap<T>(http.patch<ApiResponse<T>>(url, body ?? {}, config)),

  delete: <T = unknown>(url: string, config?: AxiosRequestConfig) => unwrap<T>(http.delete<ApiResponse<T>>(url, config)),

  /** Multipart form (files): the browser sets the boundary. */
  postForm: <T>(url: string, form: FormData) => unwrap<T>(http.post<ApiResponse<T>>(url, form)),
  putForm: <T>(url: string, form: FormData) => unwrap<T>(http.put<ApiResponse<T>>(url, form)),

  /** GET or POST that returns a file (PDF, Excel, HTML label). */
  async download(url: string, options: { params?: object; method?: 'get' | 'post'; body?: unknown; fallbackName?: string } = {}): Promise<DownloadedFile> {
    try {
      const response = await http.request<Blob>({
        url,
        method: options.method ?? 'get',
        params: options.params,
        data: options.body,
        responseType: 'blob',
        headers: { Accept: 'application/pdf, application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, text/html, application/json' }
      });
      return {
        blob: response.data,
        fileName: fileNameFrom(response.headers['content-disposition'] as string | undefined, options.fallbackName ?? 'file')
      };
    } catch (error) {
      throw await toAppError(error);
    }
  },

  /** An export endpoint: ?format=excel|pdf plus the screen filters. */
  export: (url: string, format: ExportFormat, params?: object) =>
    api.download(url, { params: { ...params, format }, fallbackName: `export.${format === 'pdf' ? 'pdf' : 'xlsx'}` })
};

/** Saves a downloaded file with its name. */
export function saveFile(file: DownloadedFile): void {
  const url = URL.createObjectURL(file.blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = file.fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 30_000);
}

/** Opens a PDF / HTML document in a new tab for printing (thermal labels, vouchers, manifests). */
export function openFile(file: DownloadedFile): void {
  const url = URL.createObjectURL(file.blob);
  const opened = window.open(url, '_blank');
  if (opened) {
    opened.opener = null;
  } else {
    saveFile(file);
  }

  setTimeout(() => URL.revokeObjectURL(url), 120_000);
}
