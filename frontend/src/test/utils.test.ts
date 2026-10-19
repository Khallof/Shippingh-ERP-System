import { describe, expect, it } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { AppError, firstFieldError, toAppError } from '@/api/errors';
import { tokenStore } from '@/api/tokenStore';
import { apiPath } from '@/api/client';
import { splitCodes } from '@/components/form/ScanBox';
import { splitPackages } from '@/features/shipments/packages';
import { enumLabel, enumOptions } from '@/i18n/enums';
import { businessDayEndUtc, businessDayStartUtc, formatDate, formatDateTime, formatMoney } from '@/utils/format';

describe('format', () => {
  it('formats money with two decimals and western digits', () => {
    expect(formatMoney(1234.5, 'SYP', 'en')).toBe('1,234.50 SYP');
    expect(formatMoney(null)).toBe('-');
  });

  it('shows UTC timestamps in the business time zone (Asia/Damascus, UTC+3)', () => {
    expect(formatDateTime('2026-10-01T21:30:00Z')).toBe('2026-10-02 00:30');
    expect(formatDateTime('2026-10-01T21:30:00')).toBe('2026-10-02 00:30');
  });

  it('keeps date-only values as they are', () => {
    expect(formatDate('2026-10-01')).toBe('2026-10-01');
    expect(formatDate('2026-10-01T00:00:00')).toBe('2026-10-01');
  });

  it('converts a business day to a UTC range', () => {
    expect(businessDayStartUtc('2026-10-02')).toBe('2026-10-01T21:00:00.000Z');
    expect(businessDayEndUtc('2026-10-02')).toBe('2026-10-02T21:00:00.000Z');
  });
});

describe('enum labels', () => {
  it('translates API enum names', () => {
    expect(enumLabel('ShipmentStatus', 'OutForDelivery', 'ar')).toBe('خارج للتوزيع');
    expect(enumLabel('ShipmentStatus', 'OutForDelivery', 'en')).toBe('Out for delivery');
    expect(enumLabel('ShipmentStatus', 'Unknown', 'ar')).toBe('Unknown');
    expect(enumOptions('CustomerType', 'en')).toEqual([{ value: 'Individual', label: 'Individual' }, { value: 'Business', label: 'Business' }]);
  });
});

describe('scan codes', () => {
  it('splits pasted codes and removes duplicates', () => {
    expect(splitCodes(' SH1\nSH2, SH1;SH3 ')).toEqual(['SH1', 'SH2', 'SH3']);
    expect(splitCodes('')).toEqual([]);
  });
});

describe('api helpers', () => {
  it('strips the API prefix from server links', () => {
    expect(apiPath('/api/v1/shipments/5/attachments/9/content')).toBe('/shipments/5/attachments/9/content');
  });

  it('maps the error envelope to AppError with field errors', async () => {
    const error = new AxiosError('bad', 'ERR', undefined, undefined, {
      status: 400,
      statusText: 'Bad Request',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { success: false, message: '', data: null, traceId: 'abc', timestampUtc: '', error: { code: 'VALIDATION_ERROR', message: 'خطأ', details: { phone: ['الهاتف مطلوب'] } } }
    });
    const appError = await toAppError(error);
    expect(appError).toBeInstanceOf(AppError);
    expect(appError.status).toBe(400);
    expect(appError.isValidation).toBe(true);
    expect(appError.traceId).toBe('abc');
    expect(firstFieldError(appError, 'phone')).toBe('الهاتف مطلوب');
  });

  it('reports a network error as status 0', async () => {
    const appError = await toAppError(new AxiosError('Network Error', 'ERR_NETWORK'));
    expect(appError.status).toBe(0);
  });
});

describe('token store', () => {
  it('keeps the access token in memory and detects expiry', () => {
    tokenStore.set('token', new Date(Date.now() + 10_000).toISOString());
    expect(tokenStore.get()).toBe('token');
    expect(tokenStore.isExpiringSoon()).toBe(true);
    tokenStore.set('token', new Date(Date.now() + 600_000).toISOString());
    expect(tokenStore.isExpiringSoon()).toBe(false);
    tokenStore.clear();
    expect(tokenStore.get()).toBeNull();
  });
});

describe('splitPackages', () => {
  it('splits the total weight in grams so the packages add up exactly', () => {
    const packages = splitPackages(3, 10);
    expect(packages.map((p) => p.weightKg)).toEqual([3.334, 3.333, 3.333]);
    expect(Math.round(packages.reduce((sum, p) => sum + (p.weightKg ?? 0), 0) * 1000)).toBe(10000);
  });

  it('gives one package the whole weight', () => {
    expect(splitPackages(1, 2.5)).toEqual([{ description: null, weightKg: 2.5, lengthCm: null, widthCm: null, heightCm: null }]);
  });
});
