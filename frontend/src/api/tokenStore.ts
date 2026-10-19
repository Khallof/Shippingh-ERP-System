/**
 * Access token storage: MEMORY ONLY (never localStorage / sessionStorage), so a script injected into the page cannot
 * read a stored token after a reload. The refresh token is an httpOnly cookie set by the API (X-Auth-Mode: cookie) and
 * is never visible to JavaScript. A page reload therefore starts without an access token and silently asks for a new
 * one with POST /auth/web/refresh.
 */
let accessToken: string | null = null;
let expiresAt = 0;
const listeners = new Set<(token: string | null) => void>();

export const tokenStore = {
  get(): string | null {
    return accessToken;
  },

  /** True when the token expires within the next 30 seconds (refresh it before calling the API). */
  isExpiringSoon(): boolean {
    return accessToken !== null && Date.now() > expiresAt - 30_000;
  },

  set(token: string | null, expiresAtUtc?: string | null): void {
    accessToken = token;
    expiresAt = token && expiresAtUtc ? Date.parse(expiresAtUtc) : 0;
    listeners.forEach((listener) => listener(token));
  },

  clear(): void {
    this.set(null);
  },

  subscribe(listener: (token: string | null) => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  }
};
