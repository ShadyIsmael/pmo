const ACCESS_TOKEN_KEY = "nexus_access_token";
const REFRESH_TOKEN_KEY = "nexus_refresh_token";
const SESSION_ID_KEY = "nexus_auth_session_id";
const API_BASE_URL = (import.meta.env.VITE_API_URL || "http://localhost:5000").replace(/\/+$/, "");

export interface RoleDto { id: string; name: string; description?: string; category?: string; }
export type AuthenticationSource = "Local" | "EntraId" | "ActiveDirectoryLdap";
export interface CurrentUser { id: string; email: string; firstName: string; lastName: string; fullName: string; jobTitle?: string; avatarUrl?: string; tenantId?: string; authenticationSource: AuthenticationSource; mustChangePassword: boolean; temporaryPasswordExpiresAt?: string | null; roles: RoleDto[]; permissions: string[]; }
export interface AuthResponse { token: string; refreshToken: string; expiresAt: string; user: CurrentUser; passwordChangeRequired: boolean; }
export interface InitialPasswordPolicy { minimumLength: number; maximumLength: number; }
export interface ManagementRequestContext { perspective: string; viewMode: "Native" | "ReadOnlyViewAs"; portfolioId?: string; programId?: string; projectId?: string; }

export class ApiError extends Error {
  constructor(public readonly status: number, message: string, public readonly code?: string) { super(message); this.name = "ApiError"; }
}

function readStorage(key: string): string | null {
  try { return typeof window === "undefined" ? null : window.localStorage.getItem(key); }
  catch { return null; }
}

function createClientSessionId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") return crypto.randomUUID();
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

let accessToken: string | null = readStorage(ACCESS_TOKEN_KEY);
let refreshToken: string | null = readStorage(REFRESH_TOKEN_KEY);
let clientSessionId: string | null = readStorage(SESSION_ID_KEY);
let sessionGeneration = 0;
let contextGeneration = 0;
const refreshPromises = new Map<string, Promise<boolean>>();
const activeRequests = new Set<AbortController>();
let managementContext: ManagementRequestContext | null = null;

function abortActiveRequests(): void {
  for (const controller of activeRequests) controller.abort();
  activeRequests.clear();
}

function advanceSession(): void {
  sessionGeneration += 1;
  contextGeneration += 1;
  managementContext = null;
  abortActiveRequests();
}

if (accessToken && refreshToken && !clientSessionId && typeof window !== "undefined") {
  clientSessionId = createClientSessionId();
  try { window.localStorage.setItem(SESSION_ID_KEY, clientSessionId); } catch { /* The in-memory session remains usable. */ }
}

export class SessionChangedError extends Error {
  constructor() { super("The authenticated session or management context changed while this request was running."); this.name = "SessionChangedError"; }
}

export function hasTokens(): boolean { return Boolean(accessToken && refreshToken && clientSessionId); }
export function getClientSessionId(): string | null { return clientSessionId; }
export function getSessionGeneration(): number { return sessionGeneration; }

export function setTokens(access: string, refresh: string): string {
  advanceSession();
  accessToken = access;
  refreshToken = refresh;
  clientSessionId = createClientSessionId();
  try {
    window.localStorage.setItem(ACCESS_TOKEN_KEY, access);
    window.localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
    window.localStorage.setItem(SESSION_ID_KEY, clientSessionId);
  } catch { /* Authentication remains in memory if browser storage is unavailable. */ }
  return clientSessionId;
}

export function clearTokens(): void {
  advanceSession();
  accessToken = null;
  refreshToken = null;
  clientSessionId = null;
  try {
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    window.localStorage.removeItem(REFRESH_TOKEN_KEY);
    window.localStorage.removeItem(SESSION_ID_KEY);
  } catch { /* Local in-memory credentials are still removed. */ }
}

/** Adopts a different tab's auth transition without mutating that tab's shared storage. */
export function syncStoredSession(): boolean {
  const storedSessionId = readStorage(SESSION_ID_KEY);
  const storedAccessToken = readStorage(ACCESS_TOKEN_KEY);
  const storedRefreshToken = readStorage(REFRESH_TOKEN_KEY);
  if (storedSessionId && storedSessionId === clientSessionId && storedAccessToken && storedRefreshToken) {
    // Refresh-token rotation may update credentials in another tab without changing the user session.
    accessToken = storedAccessToken;
    refreshToken = storedRefreshToken;
    return false;
  }

  advanceSession();
  const hasCompleteStoredSession = Boolean(storedSessionId && storedAccessToken && storedRefreshToken);
  clientSessionId = hasCompleteStoredSession ? storedSessionId : null;
  accessToken = hasCompleteStoredSession ? storedAccessToken : null;
  refreshToken = hasCompleteStoredSession ? storedRefreshToken : null;
  return true;
}

export function setManagementRequestContext(context: ManagementRequestContext | null, ownerSessionId?: string | null): boolean {
  if (ownerSessionId !== undefined && ownerSessionId !== clientSessionId) return false;
  const next = context ? { ...context } : null;
  if (JSON.stringify(next) === JSON.stringify(managementContext)) return true;
  managementContext = next;
  contextGeneration += 1;
  abortActiveRequests();
  return true;
}

function url(path: string, params?: Record<string, string | number | boolean | undefined>): string {
  const value = new URL(`${API_BASE_URL}/${path.replace(/^\/+/, "")}`);
  Object.entries(params ?? {}).forEach(([key, item]) => { if (item !== undefined) value.searchParams.set(key, String(item)); });
  return value.toString();
}

async function readError(response: Response): Promise<{ message: string; code?: string }> {
  try {
    const body = await response.json() as { message?: string; error?: string; title?: string; code?: string; errors?: Record<string, string[]> };
    const fieldMessage = Object.values(body.errors ?? {}).flat()[0];
    return { message: body.message ?? body.error ?? fieldMessage ?? body.title ?? response.statusText, code: body.code };
  } catch { return { message: response.statusText || "Request failed" }; }
}

function notifySessionExpired(): void {
  if (typeof window !== "undefined") window.dispatchEvent(new Event("nexus:session-expired"));
}

interface RequestOwner { sessionGeneration: number; sessionId: string | null; contextGeneration: number; context: ManagementRequestContext | null; }

function captureRequestOwner(): RequestOwner {
  return { sessionGeneration, sessionId: clientSessionId, contextGeneration, context: managementContext ? { ...managementContext } : null };
}

function assertRequestOwner(owner: RequestOwner): void {
  if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId || owner.contextGeneration !== contextGeneration) {
    throw new SessionChangedError();
  }
}

function makeRequestHeaders(options: RequestInit, token: string | null, context: ManagementRequestContext | null): Headers {
  const headers = new Headers(options.headers);
  if (!(typeof FormData !== "undefined" && options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (context) {
    headers.set("X-Nexus-Perspective", context.perspective);
    headers.set("X-Nexus-View-Mode", context.viewMode);
    if (context.portfolioId) headers.set("X-Nexus-Portfolio-Id", context.portfolioId);
    if (context.programId) headers.set("X-Nexus-Program-Id", context.programId);
    if (context.projectId) headers.set("X-Nexus-Project-Id", context.projectId);
  }
  return headers;
}

async function refreshAccessToken(owner: RequestOwner): Promise<boolean> {
  if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId || !owner.sessionId) return false;
  const storedSessionId = readStorage(SESSION_ID_KEY);
  if (storedSessionId !== owner.sessionId) return false;

  // Another tab may already have rotated the refresh token. Adopt that rotation
  // instead of replaying an already-revoked token or logging out the new session.
  const storedRefreshToken = readStorage(REFRESH_TOKEN_KEY);
  const storedAccessToken = readStorage(ACCESS_TOKEN_KEY);
  if (storedRefreshToken && storedAccessToken && storedRefreshToken !== refreshToken) {
    refreshToken = storedRefreshToken;
    accessToken = storedAccessToken;
    return true;
  }
  if (!refreshToken) return false;

  const attemptRefreshToken = refreshToken;
  const key = `${owner.sessionGeneration}:${owner.sessionId}:${attemptRefreshToken}`;
  const existing = refreshPromises.get(key);
  if (existing) return existing;

  const controller = new AbortController();
  activeRequests.add(controller);
  const refreshRequest = fetch(url("/api/auth/refresh"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: attemptRefreshToken }),
    signal: controller.signal,
  }).then(async (response) => {
    if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId) return false;
    if (!response.ok) {
      if (readStorage(SESSION_ID_KEY) === owner.sessionId && readStorage(REFRESH_TOKEN_KEY) === attemptRefreshToken) notifySessionExpired();
      return false;
    }

    const data = await response.json() as AuthResponse;
    if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId) return false;
    if (readStorage(SESSION_ID_KEY) !== owner.sessionId) return false;
    if (readStorage(REFRESH_TOKEN_KEY) !== attemptRefreshToken) {
      const latestAccess = readStorage(ACCESS_TOKEN_KEY);
      const latestRefresh = readStorage(REFRESH_TOKEN_KEY);
      if (latestAccess && latestRefresh) {
        accessToken = latestAccess;
        refreshToken = latestRefresh;
        return true;
      }
      return false;
    }

    accessToken = data.token;
    refreshToken = data.refreshToken;
    try {
      window.localStorage.setItem(ACCESS_TOKEN_KEY, data.token);
      window.localStorage.setItem(REFRESH_TOKEN_KEY, data.refreshToken);
    } catch { /* In-memory rotation remains valid in this tab. */ }
    return true;
  }).catch(() => false).finally(() => {
    activeRequests.delete(controller);
    refreshPromises.delete(key);
  });
  refreshPromises.set(key, refreshRequest);
  return refreshRequest;
}

function isRefreshExcluded(path: string): boolean {
  return ["/api/auth/login", "/api/auth/refresh", "/api/auth/logout"].includes(path.replace(/\/+$/, ""));
}

async function send(path: string, options: RequestInit, retry: boolean, params?: Record<string, string | number | boolean | undefined>, authenticated = true): Promise<{ response: Response; owner: RequestOwner | null }> {
  const owner = authenticated ? captureRequestOwner() : null;
  const controller = authenticated ? new AbortController() : null;
  if (controller) activeRequests.add(controller);
  const forwardedSignal = options.signal;
  const abort = () => controller?.abort();
  if (forwardedSignal?.aborted) abort();
  else forwardedSignal?.addEventListener("abort", abort, { once: true });

  try {
    let attempt = 0;
    while (true) {
      if (owner) assertRequestOwner(owner);
      const headers = makeRequestHeaders(options, authenticated ? accessToken : null, authenticated ? owner?.context ?? null : null);
      const response = await fetch(url(path, params), { ...options, headers, ...(controller ? { signal: controller.signal } : {}) });
      if (owner) assertRequestOwner(owner);
      if (response.status === 401 && retry && authenticated && !isRefreshExcluded(path)) {
        const refreshed = await refreshAccessToken(owner!);
        assertRequestOwner(owner!);
        if (refreshed && attempt === 0) {
          attempt += 1;
          retry = false;
          continue;
        }
      }
      return { response, owner };
    }
  } finally {
    forwardedSignal?.removeEventListener("abort", abort);
    if (controller) activeRequests.delete(controller);
  }
}

async function request<T>(path: string, options: RequestInit = {}, retry = true, params?: Record<string, string | number | boolean | undefined>, authenticated = true): Promise<T> {
  const { response, owner } = await send(path, options, retry, params, authenticated);
  if (!response.ok) { const error = await readError(response); if (owner) assertRequestOwner(owner); throw new ApiError(response.status, error.message, error.code); }
  if (response.status === 204) { if (owner) assertRequestOwner(owner); return undefined as T; }
  const data = await response.json() as T;
  if (owner) assertRequestOwner(owner);
  return data;
}

async function requestBinary(path: string, retry = true): Promise<Blob> {
  const { response, owner } = await send(path, { method: "GET" }, retry);
  if (!response.ok) { const error = await readError(response); if (owner) assertRequestOwner(owner); throw new ApiError(response.status, error.message, error.code); }
  const data = await response.blob();
  if (owner) assertRequestOwner(owner);
  return data;
}

async function unregisterPushDeviceDuringLogout(deviceId: string, token: string | null): Promise<void> {
  if (!token || !/^[0-9a-f-]{36}$/i.test(deviceId)) return;
  const response = await fetch(url(`/api/notifications/devices/${encodeURIComponent(deviceId)}`), {
    method: "DELETE",
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok && response.status !== 401 && response.status !== 404) {
    const error = await readError(response);
    throw new ApiError(response.status, error.message, error.code);
  }
}

async function revokeSession(refreshAtLogout: string | null, accessAtLogout: string | null): Promise<void> {
  if (!refreshAtLogout || !accessAtLogout) return;
  const response = await fetch(url("/api/auth/logout"), {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${accessAtLogout}` },
    body: JSON.stringify({ refreshToken: refreshAtLogout }),
  });
  if (!response.ok && response.status !== 401) {
    const error = await readError(response);
    throw new ApiError(response.status, error.message, error.code);
  }
}

export const api = {
  get: <T>(path: string, params?: Record<string, string | number | boolean | undefined>) => request<T>(path, { method: "GET" }, true, params),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: "PUT", body: body === undefined ? undefined : JSON.stringify(body) }),
  patch: <T>(path: string, body?: unknown) => request<T>(path, { method: "PATCH", body: body === undefined ? undefined : JSON.stringify(body) }),
  upload: <T>(path: string, body: FormData) => request<T>(path, { method: "POST", body }),
  download: <T extends Blob = Blob>(path: string) => requestBinary(path) as Promise<T>,
  delete: <T>(path: string, body?: unknown, params?: Record<string, string | number | boolean | undefined>) => request<T>(path, { method: "DELETE", body: body === undefined ? undefined : JSON.stringify(body) }, true, params),
  login: (email: string, password: string) => request<AuthResponse>("/api/auth/login", { method: "POST", body: JSON.stringify({ email, password, tenantId: import.meta.env.VITE_TENANT_ID || undefined }) }, false, undefined, false),
  getInitialPasswordPolicy: () => request<InitialPasswordPolicy>("/api/auth/initial-password-policy"),
  changeInitialPassword: (currentPassword: string, newPassword: string) => request<{ reauthenticationRequired: boolean }>("/api/auth/change-initial-password", { method: "POST", body: JSON.stringify({ currentPassword, newPassword }) }),
  logout: (beforeRevoke?: (accessToken: string | null) => Promise<void>) => {
    const capturedAccessToken = accessToken;
    const capturedRefreshToken = refreshToken;
    return (async () => {
      const revocation = revokeSession(capturedRefreshToken, capturedAccessToken);
      const deviceCleanup = Promise.resolve().then(() => beforeRevoke?.(capturedAccessToken));
      const [revokeResult] = await Promise.allSettled([revocation, deviceCleanup]);
      if (revokeResult.status === "rejected") throw revokeResult.reason;
    })();
  },
  unregisterPushDeviceForLogout: (deviceId: string, capturedAccessToken: string | null) => unregisterPushDeviceDuringLogout(deviceId, capturedAccessToken),
  getMe: () => request<CurrentUser>("/api/auth/me"),
};
