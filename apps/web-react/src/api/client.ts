const ACCESS_TOKEN_KEY = "nexus_access_token";
const REFRESH_TOKEN_KEY = "nexus_refresh_token";
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

let accessToken: string | null = typeof window === "undefined" ? null : window.localStorage.getItem(ACCESS_TOKEN_KEY);
let refreshToken: string | null = typeof window === "undefined" ? null : window.localStorage.getItem(REFRESH_TOKEN_KEY);
let refreshPromise: Promise<boolean> | null = null;
let managementContext: ManagementRequestContext | null = null;

export function hasTokens(): boolean { return Boolean(accessToken && refreshToken); }
export function setTokens(access: string, refresh: string): void { accessToken = access; refreshToken = refresh; window.localStorage.setItem(ACCESS_TOKEN_KEY, access); window.localStorage.setItem(REFRESH_TOKEN_KEY, refresh); }
export function clearTokens(): void { accessToken = null; refreshToken = null; window.localStorage.removeItem(ACCESS_TOKEN_KEY); window.localStorage.removeItem(REFRESH_TOKEN_KEY); }
export function setManagementRequestContext(context: ManagementRequestContext | null): void { managementContext = context; }

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

async function refreshAccessToken(): Promise<boolean> {
  if (!refreshToken) return false;
  if (refreshPromise) return refreshPromise;
  refreshPromise = fetch(url("/api/auth/refresh"), { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken }) })
    .then(async (response) => {
      if (!response.ok) { clearTokens(); notifySessionExpired(); return false; }
      const data = await response.json() as AuthResponse;
      setTokens(data.token, data.refreshToken);
      return true;
    })
    .catch(() => false)
    .finally(() => { refreshPromise = null; });
  return refreshPromise;
}

async function request<T>(path: string, options: RequestInit = {}, retry = true, params?: Record<string, string | number | boolean | undefined>): Promise<T> {
  const headers = new Headers(options.headers);
  if (!(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
  if (managementContext) {
    headers.set("X-Nexus-Perspective", managementContext.perspective);
    headers.set("X-Nexus-View-Mode", managementContext.viewMode);
    if (managementContext.portfolioId) headers.set("X-Nexus-Portfolio-Id", managementContext.portfolioId);
    if (managementContext.programId) headers.set("X-Nexus-Program-Id", managementContext.programId);
    if (managementContext.projectId) headers.set("X-Nexus-Project-Id", managementContext.projectId);
  }
  const response = await fetch(url(path, params), { ...options, headers });
  if (response.status === 401 && retry && !path.includes("/auth/") && await refreshAccessToken()) return request<T>(path, options, false, params);
  if (!response.ok) { const error = await readError(response); throw new ApiError(response.status, error.message, error.code); }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
}

async function requestBinary(path: string, retry = true): Promise<Blob> {
  const headers = new Headers();
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);
  if (managementContext) {
    headers.set("X-Nexus-Perspective", managementContext.perspective);
    headers.set("X-Nexus-View-Mode", managementContext.viewMode);
    if (managementContext.portfolioId) headers.set("X-Nexus-Portfolio-Id", managementContext.portfolioId);
    if (managementContext.programId) headers.set("X-Nexus-Program-Id", managementContext.programId);
    if (managementContext.projectId) headers.set("X-Nexus-Project-Id", managementContext.projectId);
  }
  const response = await fetch(url(path), { method: "GET", headers });
  if (response.status === 401 && retry && !path.includes("/auth/") && await refreshAccessToken()) return requestBinary(path, false);
  if (!response.ok) { const error = await readError(response); throw new ApiError(response.status, error.message, error.code); }
  return response.blob();
}

export const api = {
  get: <T>(path: string, params?: Record<string, string | number | boolean | undefined>) => request<T>(path, { method: "GET" }, true, params),
  post: <T>(path: string, body?: unknown) => request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body?: unknown) => request<T>(path, { method: "PUT", body: body === undefined ? undefined : JSON.stringify(body) }),
  patch: <T>(path: string, body?: unknown) => request<T>(path, { method: "PATCH", body: body === undefined ? undefined : JSON.stringify(body) }),
  upload: <T>(path: string, body: FormData) => request<T>(path, { method: "POST", body }),
  download: <T extends Blob = Blob>(path: string) => requestBinary(path) as Promise<T>,
  delete: <T>(path: string, body?: unknown, params?: Record<string, string | number | boolean | undefined>) => request<T>(path, { method: "DELETE", body: body === undefined ? undefined : JSON.stringify(body) }, true, params),
  login: (email: string, password: string) => request<AuthResponse>("/api/auth/login", { method: "POST", body: JSON.stringify({ email, password, tenantId: import.meta.env.VITE_TENANT_ID || undefined }) }, false),
  getInitialPasswordPolicy: () => request<InitialPasswordPolicy>("/api/auth/initial-password-policy"),
  changeInitialPassword: (currentPassword: string, newPassword: string) => request<{ reauthenticationRequired: boolean }>("/api/auth/change-initial-password", { method: "POST", body: JSON.stringify({ currentPassword, newPassword }) }),
  logout: (token: string | null) => request<void>("/api/auth/logout", { method: "POST", body: JSON.stringify({ refreshToken: token }) }, false),
  getMe: () => request<CurrentUser>("/api/auth/me"),
};
