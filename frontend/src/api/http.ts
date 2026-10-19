import axios, { AxiosError, type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios';
import { tokenStore } from './tokenStore';
import type { ApiResponse } from './types';
import type { AuthResponse } from './generated';

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/v1';

/** Header that asks the API for a browser session: refresh token in an httpOnly cookie, never in the body. */
export const AUTH_MODE_HEADER = { 'X-Auth-Mode': 'cookie' } as const;

/**
 * The axios instance of the app.
 *   - Authorization: Bearer <access token from memory>
 *   - The access token is renewed BEFORE it expires (30 s margin) and once more after a 401, with a single shared
 *     refresh call (many parallel requests wait for the same one).
 *   - Arrays in query strings are sent as ids=1&ids=2 (ASP.NET Core binding); empty values are dropped.
 */
export const http = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  timeout: 60_000,
  headers: { Accept: 'application/json', 'Accept-Language': 'ar' },
  paramsSerializer: {
    serialize: (params: Record<string, unknown>) => {
      const search = new URLSearchParams();
      Object.entries(params).forEach(([key, value]) => {
        if (value === undefined || value === null || value === '') {
          return;
        }

        if (Array.isArray(value)) {
          value.forEach((item) => search.append(key, String(item)));
        } else {
          search.append(key, String(value));
        }
      });
      return search.toString();
    }
  }
});

let refreshPromise: Promise<string | null> | null = null;
let sessionExpiredHandler: (() => void) | null = null;

/** Called by the AuthProvider: what to do when the session cannot be renewed (go to the login page). */
export function onSessionExpired(handler: () => void): void {
  sessionExpiredHandler = handler;
}

/** Asks the API for a new access token with the refresh cookie. Returns null when the session is over. */
export function refreshAccessToken(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = axios
      .post<ApiResponse<AuthResponse>>(`${API_BASE_URL}/auth/web/refresh`, null, { withCredentials: true, headers: AUTH_MODE_HEADER })
      .then((response) => {
        const data = response.data.data;
        if (!data) {
          return null;
        }

        tokenStore.set(data.accessToken, data.accessTokenExpiresAtUtc);
        return data.accessToken;
      })
      .catch(() => {
        tokenStore.clear();
        return null;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
}

const isAuthCall = (url?: string) => !!url && /\/auth\/(login|web\/refresh|web\/logout)/.test(url);

http.interceptors.request.use(async (config: InternalAxiosRequestConfig) => {
  if (!isAuthCall(config.url) && tokenStore.isExpiringSoon()) {
    await refreshAccessToken();
  }

  const token = tokenStore.get();
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }

  config.headers.set('Accept-Language', document.documentElement.lang || 'ar');
  return config;
});

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined;
    if (error.response?.status === 401 && original && !original._retried && !isAuthCall(original.url)) {
      original._retried = true;
      const token = await refreshAccessToken();
      if (token) {
        return http(original);
      }

      sessionExpiredHandler?.();
    }

    return Promise.reject(error);
  }
);
