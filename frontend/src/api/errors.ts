import axios from 'axios';
import type { ApiErrorBody, ApiResponse } from './types';

/**
 * One error type for the whole app. status 0 = network error (API unreachable).
 * fieldErrors: camelCase field name -> messages, ready for react-hook-form setError.
 */
export class AppError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly traceId: string | null;

  constructor(status: number, code: string, message: string, fieldErrors: Record<string, string[]> = {}, traceId: string | null = null) {
    super(message);
    this.name = 'AppError';
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
    this.traceId = traceId;
  }

  get isValidation(): boolean {
    return this.status === 400 && Object.keys(this.fieldErrors).length > 0;
  }
}

const statusMessages: Record<number, string> = {
  0: 'تعذّر الاتصال بالخادم. تحقق من الشبكة أو من تشغيل الواجهة البرمجية.',
  401: 'انتهت الجلسة. سجّل الدخول من جديد.',
  403: 'لا تملك صلاحية تنفيذ هذه العملية.',
  404: 'العنصر غير موجود أو لا يمكنك الوصول إليه.',
  409: 'تعارض: تم تعديل البيانات من مستخدم آخر أو العملية مكررة.',
  413: 'حجم الملف أكبر من المسموح.',
  422: 'لا يمكن تنفيذ العملية بسبب قاعدة عمل.',
  429: 'طلبات كثيرة. انتظر قليلاً ثم أعد المحاولة.',
  500: 'حدث خطأ غير متوقع في الخادم.'
};

/** Converts anything thrown by axios (JSON envelope, blob envelope, network error) into an AppError. */
export async function toAppError(error: unknown): Promise<AppError> {
  if (error instanceof AppError) {
    return error;
  }

  if (axios.isAxiosError(error)) {
    const status = error.response?.status ?? 0;
    let body: Partial<ApiResponse<unknown>> | undefined = error.response?.data as Partial<ApiResponse<unknown>> | undefined;

    // File downloads use responseType "blob": the JSON error arrives as a Blob.
    if (body instanceof Blob) {
      try {
        body = JSON.parse(await body.text()) as Partial<ApiResponse<unknown>>;
      } catch {
        body = undefined;
      }
    }

    const apiError: ApiErrorBody | null | undefined = body?.error;
    const message = apiError?.message || statusMessages[status] || statusMessages[500];
    return new AppError(status, apiError?.code ?? `HTTP_${status}`, message, apiError?.details ?? {}, body?.traceId ?? null);
  }

  return new AppError(500, 'CLIENT_ERROR', error instanceof Error ? error.message : statusMessages[500]);
}

/** The first message of a field, or of any field. */
export function firstFieldError(error: AppError, field?: string): string | undefined {
  if (field) {
    return error.fieldErrors[field]?.[0];
  }

  return Object.values(error.fieldErrors)[0]?.[0];
}
