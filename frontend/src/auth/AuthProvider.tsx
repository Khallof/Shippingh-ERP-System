import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { API_BASE_URL, AUTH_MODE_HEADER, http, onSessionExpired, refreshAccessToken } from '@/api/http';
import { toAppError } from '@/api/errors';
import { tokenStore } from '@/api/tokenStore';
import type { ApiResponse } from '@/api/types';
import type { AuthResponse, ChangePasswordRequest, CurrentUserDto } from '@/api/generated';

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous';

interface AuthContextValue {
  status: AuthStatus;
  user: CurrentUserDto | null;
  /** Shown once on the login page after an expired session. */
  sessionExpired: boolean;
  login(userNameOrEmail: string, password: string): Promise<void>;
  logout(): Promise<void>;
  changePassword(request: ChangePasswordRequest): Promise<void>;
  reloadUser(): Promise<void>;
  hasPermission(permission: string): boolean;
  hasAny(...permissions: string[]): boolean;
  hasRole(role: string): boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Auth flow (browser):
 *   1. Login: POST /auth/login with X-Auth-Mode: cookie. The API returns the access token (kept in memory) and sets the
 *      refresh token as an httpOnly, Secure, SameSite=Strict cookie limited to /api/v1/auth/web.
 *   2. Page load: POST /auth/web/refresh. A valid cookie gives a new access token (and rotates the cookie); then GET
 *      /auth/me loads the user, roles and permissions.
 *   3. Every request: Bearer token from memory; renewed 30 s before it expires and once after a 401 (http.ts).
 *   4. Logout: POST /auth/web/logout revokes the refresh token and deletes the cookie; the query cache is cleared.
 * The user's permissions only show or hide screens; the API checks every call again.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<CurrentUserDto | null>(null);
  const [sessionExpired, setSessionExpired] = useState(false);

  const signedOut = useCallback(
    (expired: boolean) => {
      tokenStore.clear();
      setUser(null);
      setStatus('anonymous');
      setSessionExpired(expired);
      queryClient.clear();
    },
    [queryClient]
  );

  const loadUser = useCallback(async () => {
    const response = await http.get<ApiResponse<CurrentUserDto>>('/auth/me');
    setUser(response.data.data);
    setStatus('authenticated');
  }, []);

  // Silent sign-in on page load with the refresh cookie.
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const token = await refreshAccessToken();
      if (cancelled) {
        return;
      }

      if (!token) {
        setStatus('anonymous');
        return;
      }

      try {
        await loadUser();
      } catch {
        if (!cancelled) {
          signedOut(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [loadUser, signedOut]);

  useEffect(() => onSessionExpired(() => signedOut(true)), [signedOut]);

  const login = useCallback(
    async (userNameOrEmail: string, password: string) => {
      try {
        const response = await axios.post<ApiResponse<AuthResponse>>(
          `${API_BASE_URL}/auth/login`,
          { userNameOrEmail, password },
          { withCredentials: true, headers: { ...AUTH_MODE_HEADER, 'Accept-Language': document.documentElement.lang || 'ar' } }
        );
        const data = response.data.data!;
        tokenStore.set(data.accessToken, data.accessTokenExpiresAtUtc);
        queryClient.clear();
        setUser(data.user);
        setSessionExpired(false);
        setStatus('authenticated');
      } catch (error) {
        throw await toAppError(error);
      }
    },
    [queryClient]
  );

  const logout = useCallback(async () => {
    try {
      await axios.post(`${API_BASE_URL}/auth/web/logout`, null, { withCredentials: true, headers: AUTH_MODE_HEADER });
    } catch {
      // The local session ends even when the API is unreachable; the cookie then expires on its own.
    }

    signedOut(false);
  }, [signedOut]);

  const changePassword = useCallback(async (request: ChangePasswordRequest) => {
    try {
      // Cookie mode: the API revokes the other sessions and sets a NEW refresh cookie for this browser.
      const response = await http.post<ApiResponse<AuthResponse>>('/auth/change-password', request, { headers: AUTH_MODE_HEADER });
      const data = response.data.data;
      if (data) {
        tokenStore.set(data.accessToken, data.accessTokenExpiresAtUtc);
        setUser(data.user);
      }
    } catch (error) {
      throw await toAppError(error);
    }
  }, []);

  const value = useMemo<AuthContextValue>(() => {
    const permissions = new Set(user?.permissions ?? []);
    return {
      status,
      user,
      sessionExpired,
      login,
      logout,
      changePassword,
      reloadUser: loadUser,
      hasPermission: (permission) => permissions.has(permission),
      hasAny: (...list) => list.length === 0 || list.some((permission) => permissions.has(permission)),
      hasRole: (role) => !!user?.roles.includes(role)
    };
  }, [status, user, sessionExpired, login, logout, changePassword, loadUser]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used inside <AuthProvider>.');
  }

  return context;
}
