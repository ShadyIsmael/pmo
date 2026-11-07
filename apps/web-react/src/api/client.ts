import { getLocalizedApiErrorMessage, getLocalizedApiFieldError, type ApiErrorLocale } from "./api-error-localization";

const ACCESS_TOKEN_KEY = "nexus_access_token";
const REFRESH_TOKEN_KEY = "nexus_refresh_token";
const SESSION_ID_KEY = "nexus_auth_session_id";
const API_BASE_URL = (import.meta.env.VITE_API_URL || "").replace(/\/+$/, "");

export interface RoleDto { id: string; name: string; description?: string; category?: string; }
export type AuthenticationSource = "Local" | "EntraId" | "ActiveDirectoryLdap";
export interface CurrentUser { id: string; email: string; firstName: string; lastName: string; fullName: string; jobTitle?: string; avatarUrl?: string; tenantId?: string; authenticationSource: AuthenticationSource; mustChangePassword: boolean; temporaryPasswordExpiresAt?: string | null; roles: RoleDto[]; permissions: string[]; }
export interface AuthResponse { token: string; expiresAt: string; user: CurrentUser; passwordChangeRequired: boolean; }
export interface InitialPasswordPolicy { minimumLength: number; maximumLength: number; }
export interface ManagementRequestContext { perspective: string; viewMode: "Native" | "ReadOnlyViewAs"; portfolioId?: string; programId?: string; projectId?: string; }

export class ApiError extends Error {
  public readonly summary: string;

  constructor(public readonly status: number, message: string, public readonly code?: string, public readonly details?: unknown) {
    const fieldMessages = status < 500
      ? Object.entries(readFieldErrors(details)).flatMap(([field, messages]) =>
        messages.map((fieldMessage) => code === "HANDOVER_DATE_AFTER_PROJECT_END"
          ? getLocalizedApiErrorMessage(code, status, apiErrorLocale, getProblemParameters(details))
          : getLocalizedApiFieldError(code, status, apiErrorLocale, field, fieldMessage)))
      : [];
    super([...new Set([message, ...fieldMessages])].join(" "));
    this.name = "ApiError";
    this.summary = message;
  }

  get fieldErrors(): Record<string, string[]> {
    const errors = readFieldErrors(this.details);
    return Object.fromEntries(
      Object.entries(errors).flatMap(([field, messages]) => {
        const localized = messages.map((message) => this.code === "HANDOVER_DATE_AFTER_PROJECT_END"
          ? getLocalizedApiErrorMessage(this.code, this.status, apiErrorLocale, getProblemParameters(this.details))
          : getLocalizedApiFieldError(this.code, this.status, apiErrorLocale, field, message));
        return localized.length > 0 ? [[field, localized]] : [];
      }),
    );
  }
}

let apiErrorLocale: ApiErrorLocale = "en";

export function setApiErrorLocale(locale: ApiErrorLocale): void {
  apiErrorLocale = locale;
}

export function getApiFieldError(error: unknown, fieldName: string): string | undefined {
  if (!(error instanceof ApiError)) return undefined;
  const match = Object.entries(readFieldErrors(error.details))
    .find(([field]) => field.toLocaleLowerCase() === fieldName.toLocaleLowerCase());
  if (!match) return undefined;
  if (error.code === "HANDOVER_DATE_AFTER_PROJECT_END")
    return getLocalizedApiErrorMessage(error.code, error.status, apiErrorLocale, getProblemParameters(error.details));
  return getLocalizedApiFieldError(error.code, error.status, apiErrorLocale, match[0], match[1][0]);
}

export function getApiFieldErrors(error: unknown): Record<string, string[]> {
  return error instanceof ApiError ? error.fieldErrors : {};
}

function readStorage(key: string): string | null {
  try { return typeof window === "undefined" ? null : window.localStorage.getItem(key); }
  catch { return null; }
}

function removeLegacyTokenStorage(): void {
  try {
    if (typeof window === "undefined") return;
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    window.localStorage.removeItem(REFRESH_TOKEN_KEY);
  } catch { /* Old credentials are never read into the new session. */ }
}

function createClientSessionId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") return crypto.randomUUID();
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

removeLegacyTokenStorage();

let accessToken: string | null = null;
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

export class SessionChangedError extends Error {
  constructor() { super("The authenticated session or management context changed while this request was running."); this.name = "SessionChangedError"; }
}

// A session ID is only a local coordination value; credentials stay in memory or in
// the server's HttpOnly refresh cookie. A page reload resumes through that cookie.
export function hasSession(): boolean { return Boolean(clientSessionId); }
export function getClientSessionId(): string | null { return clientSessionId; }
export function getSessionGeneration(): number { return sessionGeneration; }

export function setTokens(access: string): string {
  advanceSession();
  accessToken = access;
  clientSessionId = createClientSessionId();
  try {
    window.localStorage.setItem(SESSION_ID_KEY, clientSessionId);
  } catch { /* The in-memory session remains usable for this tab. */ }
  return clientSessionId;
}

export function clearTokens(): void {
  advanceSession();
  accessToken = null;
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
  if (storedSessionId === clientSessionId) return false;

  advanceSession();
  clientSessionId = storedSessionId;
  accessToken = null;
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
  const requestPath = `/${path.replace(/^\/+/, "")}`;
  const currentOrigin = typeof window === "undefined" ? "http://localhost" : window.location.origin;
  const value = API_BASE_URL
    ? new URL(`${API_BASE_URL}/${path.replace(/^\/+/, "")}`)
    : new URL(requestPath, currentOrigin);
  Object.entries(params ?? {}).forEach(([key, item]) => { if (item !== undefined) value.searchParams.set(key, String(item)); });
  return value.toString();
}

async function readError(response: Response): Promise<{ message: string; code?: string; details?: unknown }> {
  try {
    const body = await response.json() as Record<string, unknown>;
    const code = stringProperty(body, "code") ?? stringProperty(body, "errorCode");
    const parameters = getProblemParameters(body);
    const serverMessage = stringProperty(body, "detail")
      ?? stringProperty(body, "message")
      ?? stringProperty(body, "error");
    return {
      message: getLocalizedApiErrorMessage(code, response.status, apiErrorLocale, parameters, serverMessage),
      code,
      details: body,
    };
  } catch {
    return {
      message: getLocalizedApiErrorMessage(undefined, response.status, apiErrorLocale),
      code: undefined,
    };
  }
}

async function getCsrfToken(): Promise<string> {
  let response: Response;
  try {
    response = await fetch(url("/api/auth/csrf"), {
      method: "GET",
      credentials: "include",
      headers: { Accept: "application/json" },
      cache: "no-store",
    });
  } catch (error) {
    if (!(error instanceof TypeError)) throw error;
    throw new ApiError(0, getLocalizedApiErrorMessage("NETWORK_ERROR", 0, apiErrorLocale), "NETWORK_ERROR");
  }
  if (!response.ok) {
    const error = await readError(response);
    throw new ApiError(response.status, error.message, error.code, error.details);
  }
  const body = await response.json() as { csrfToken?: unknown };
  if (typeof body.csrfToken !== "string" || body.csrfToken.length === 0)
    throw new ApiError(0, getLocalizedApiErrorMessage("NETWORK_ERROR", 0, apiErrorLocale), "NETWORK_ERROR");
  return body.csrfToken;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function stringProperty(value: Record<string, unknown>, key: string): string | undefined {
  const entry = value[key];
  return typeof entry === "string" ? entry : undefined;
}

function getProblemParameters(value: unknown): unknown {
  if (!isRecord(value)) return undefined;
  const parameters = value.details ?? value.parameters;
  return isRecord(parameters) ? parameters : undefined;
}

function readFieldErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value) || !isRecord(value.errors)) return {};
  return Object.fromEntries(
    Object.entries(value.errors).flatMap(([field, messages]) => {
      if (typeof messages === "string") return [[field, [messages]]];
      if (Array.isArray(messages)) {
        const validMessages = messages.filter((message): message is string => typeof message === "string");
        return validMessages.length > 0 ? [[field, validMessages]] : [];
      }
      return [];
    }),
  );
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
  const key = `${owner.sessionGeneration}:${owner.sessionId}`;
  const existing = refreshPromises.get(key);
  if (existing) return existing;

  const controller = new AbortController();
  activeRequests.add(controller);
  const refreshRequest = (async () => {
    const refreshWithCookie = async (): Promise<boolean> => {
      if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId) return false;
      const csrfToken = await getCsrfToken();
      const response = await fetch(url("/api/auth/refresh"), {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json", "X-Nexus-CSRF": csrfToken, "X-Nexus-Auth-Mode": "cookie" },
        body: "{}",
        signal: controller.signal,
        cache: "no-store",
      });
      if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId) return false;
      if (!response.ok) {
        if (readStorage(SESSION_ID_KEY) === owner.sessionId) notifySessionExpired();
        return false;
      }

      const data = await response.json() as AuthResponse;
      if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId) return false;
      if (readStorage(SESSION_ID_KEY) !== owner.sessionId) return false;
      accessToken = data.token;
      return Boolean(accessToken);
    };

    const runWithRefreshLock = async (): Promise<boolean> => {
      if (owner.sessionGeneration !== sessionGeneration || owner.sessionId !== clientSessionId) return false;
      if (typeof navigator !== "undefined" && navigator.locks?.request) {
        return navigator.locks.request("nexus-auth-refresh", { mode: "exclusive" }, refreshWithCookie);
      }
      return refreshWithCookie();
    };

    try {
      return await runWithRefreshLock();
    } catch {
      return false;
    }
  })().finally(() => {
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
      const response = await fetch(url(path, params), { ...options, headers, credentials: "include", ...(controller ? { signal: controller.signal } : {}) });
      if (owner) assertRequestOwner(owner);
      if (response.status === 403 && owner && !path.startsWith("/api/auth/") && typeof window !== "undefined")
        window.dispatchEvent(new Event("nexus:authorization-denied"));
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
  let response: Response;
  let owner: RequestOwner | null;
  try {
    ({ response, owner } = await send(path, options, retry, params, authenticated));
  } catch (error) {
    if (error instanceof SessionChangedError || (error instanceof Error && error.name === "AbortError"))
      throw error;
    if (error instanceof ApiError) throw error;
    if (!(error instanceof TypeError)) throw error;
    throw new ApiError(
      0,
      getLocalizedApiErrorMessage("NETWORK_ERROR", 0, apiErrorLocale),
      "NETWORK_ERROR",
    );
  }
  if (!response.ok) { const error = await readError(response); if (owner) assertRequestOwner(owner); throw new ApiError(response.status, error.message, error.code, error.details); }
  if (response.status === 204) { if (owner) assertRequestOwner(owner); return undefined as T; }
  const data = await response.json() as T;
  if (owner) assertRequestOwner(owner);
  return data;
}

async function requestBinary(path: string, retry = true): Promise<Blob> {
  let response: Response;
  let owner: RequestOwner | null;
  try {
    ({ response, owner } = await send(path, { method: "GET" }, retry));
  } catch (error) {
    if (error instanceof SessionChangedError || (error instanceof Error && error.name === "AbortError"))
      throw error;
    if (error instanceof ApiError) throw error;
    if (!(error instanceof TypeError)) throw error;
    throw new ApiError(
      0,
      getLocalizedApiErrorMessage("NETWORK_ERROR", 0, apiErrorLocale),
      "NETWORK_ERROR",
    );
  }
  if (!response.ok) { const error = await readError(response); if (owner) assertRequestOwner(owner); throw new ApiError(response.status, error.message, error.code, error.details); }
  const data = await response.blob();
  if (owner) assertRequestOwner(owner);
  return data;
}

async function unregisterPushDeviceDuringLogout(deviceId: string, token: string | null): Promise<void> {
  if (!token || !/^[0-9a-f-]{36}$/i.test(deviceId)) return;
  const response = await fetch(url(`/api/notifications/devices/${encodeURIComponent(deviceId)}`), {
    method: "DELETE",
    credentials: "include",
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok && response.status !== 401 && response.status !== 404) {
    const error = await readError(response);
    throw new ApiError(response.status, error.message, error.code, error.details);
  }
}

async function revokeSession(accessAtLogout: string | null): Promise<void> {
  const csrfToken = await getCsrfToken();
  const headers = new Headers({
    "Content-Type": "application/json",
    "X-Nexus-CSRF": csrfToken,
    "X-Nexus-Auth-Mode": "cookie",
  });
  if (accessAtLogout) headers.set("Authorization", `Bearer ${accessAtLogout}`);
  const response = await fetch(url("/api/auth/logout"), {
    method: "POST",
    credentials: "include",
    headers,
    body: "{}",
    cache: "no-store",
  });
  if (!response.ok && response.status !== 401) {
    const error = await readError(response);
    throw new ApiError(response.status, error.message, error.code, error.details);
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
  login: async (email: string, password: string) => {
    const csrfToken = await getCsrfToken();
    return request<AuthResponse>("/api/auth/login", {
      method: "POST",
      headers: { "X-Nexus-CSRF": csrfToken, "X-Nexus-Auth-Mode": "cookie" },
      body: JSON.stringify({ email, password, tenantId: import.meta.env.VITE_TENANT_ID || undefined }),
    }, false, undefined, false);
  },
  getInitialPasswordPolicy: () => request<InitialPasswordPolicy>("/api/auth/initial-password-policy"),
  changeInitialPassword: (currentPassword: string, newPassword: string) => request<{ reauthenticationRequired: boolean }>("/api/auth/change-initial-password", { method: "POST", body: JSON.stringify({ currentPassword, newPassword }) }),
  logout: (beforeRevoke?: (accessToken: string | null) => Promise<void>) => {
    const capturedAccessToken = accessToken;
    return (async () => {
      const revocation = revokeSession(capturedAccessToken);
      const deviceCleanup = Promise.resolve().then(() => beforeRevoke?.(capturedAccessToken));
      const [revokeResult] = await Promise.allSettled([revocation, deviceCleanup]);
      if (revokeResult.status === "rejected") throw revokeResult.reason;
    })();
  },
  unregisterPushDeviceForLogout: (deviceId: string, capturedAccessToken: string | null) => unregisterPushDeviceDuringLogout(deviceId, capturedAccessToken),
  getMe: () => request<CurrentUser>("/api/auth/me"),
};
