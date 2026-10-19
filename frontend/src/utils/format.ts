import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import timezone from 'dayjs/plugin/timezone';

dayjs.extend(utc);
dayjs.extend(timezone);

/**
 * Dates: the API stores and returns UTC ("...Z" or without offset). The screens show the business time zone
 * (VITE_BUSINESS_TIME_ZONE, default Asia/Damascus, the same as the API's Shipping:BusinessTimeZone).
 * Date-only values (fromDate, invoice date) are "YYYY-MM-DD" and are never shifted.
 */
export const BUSINESS_TIME_ZONE = import.meta.env.VITE_BUSINESS_TIME_ZONE || 'Asia/Damascus';

const numberLocale = (language: string) => (language === 'en' ? 'en-US' : 'ar-SY-u-nu-latn');

/** Western digits in both languages: amounts are copied into Excel and bank forms. */
export function formatMoney(value: number | null | undefined, currencyCode?: string | null, language = 'ar'): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '-';
  }

  const text = new Intl.NumberFormat(numberLocale(language), { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  return currencyCode ? `${text} ${currencyCode}` : text;
}

export function formatNumber(value: number | null | undefined, digits = 0, language = 'ar'): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '-';
  }

  return new Intl.NumberFormat(numberLocale(language), { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
}

export function formatPercent(value: number | null | undefined, language = 'ar'): string {
  return value === null || value === undefined ? '-' : `${formatNumber(value, 1, language)}%`;
}

function asUtc(value: string): dayjs.Dayjs {
  // "2026-10-01T08:00:00" without a zone is UTC on this API.
  return /[zZ]|[+-]\d\d:\d\d$/.test(value) ? dayjs(value) : dayjs.utc(value);
}

export function formatDateTime(value: string | null | undefined): string {
  return value ? asUtc(value).tz(BUSINESS_TIME_ZONE).format('YYYY-MM-DD HH:mm') : '-';
}

export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  // A date-only value stays as it is; a timestamp is converted to the business day.
  return /^\d{4}-\d{2}-\d{2}$/.test(value.slice(0, 10)) && value.length <= 10
    ? value
    : value.includes('T') && !/T00:00:00(\.0+)?$/.test(value)
      ? asUtc(value).tz(BUSINESS_TIME_ZONE).format('YYYY-MM-DD')
      : value.slice(0, 10);
}

/** Today in the business time zone, as "YYYY-MM-DD" (default "to" date of reports). */
export function businessToday(): string {
  return dayjs().tz(BUSINESS_TIME_ZONE).format('YYYY-MM-DD');
}

export function businessDaysAgo(days: number): string {
  return dayjs().tz(BUSINESS_TIME_ZONE).subtract(days, 'day').format('YYYY-MM-DD');
}

/** Start of a business day as a UTC ISO string (list filters such as createdFromUtc). */
export function businessDayStartUtc(date: string): string {
  return dayjs.tz(date, BUSINESS_TIME_ZONE).startOf('day').utc().toISOString();
}

/** End (exclusive) of a business day as a UTC ISO string. */
export function businessDayEndUtc(date: string): string {
  return dayjs.tz(date, BUSINESS_TIME_ZONE).add(1, 'day').startOf('day').utc().toISOString();
}

/** A datetime-local input value ("2026-10-01T09:30", business time) to UTC ISO. */
export function localInputToUtc(value: string | null | undefined): string | null {
  return value ? dayjs.tz(value, BUSINESS_TIME_ZONE).utc().toISOString() : null;
}
