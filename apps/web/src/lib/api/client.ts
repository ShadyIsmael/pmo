import { config } from "@/lib/config";
import { endpoints } from "@/lib/api/endpoints";
import type { ManagementRequestContext, PerspectiveSnapshot } from "@/lib/perspective";

const ACCESS_TOKEN_KEY = "nexus_access_token";
const REFRESH_TOKEN_KEY = "nexus_refresh_token";

export class ApiError extends Error {
  status: number;
  data?: unknown;
  /** Per-field messages from an ASP.NET `ValidationProblemDetails` response. */
  fieldErrors: Record<string, string[]>;

  constructor(status: number, message: string, data?: unknown, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.data = data;
    this.fieldErrors = fieldErrors;
  }

  /** First message reported for a field, if the server sent one. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0];
  }
}

export interface RoleDto {
  id: string;
  name: string;
  description?: string;
  category?: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  jobTitle?: string;
  phone?: string;
  avatarUrl?: string;
  tenantId?: string;
  roles: RoleDto[];
  permissions: string[];
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: CurrentUser;
}

export interface PaginationParams {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: "asc" | "desc";
  search?: string;
  [key: string]: string | number | boolean | undefined;
}

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

interface RequestOptions {
  method: string;
  headers?: Record<string, string>;
  body?: unknown;
  params?: Record<string, string | number | boolean | undefined>;
  responseType?: "json" | "blob";
}

let accessToken: string | null = null;
let refreshToken: string | null = null;
let perspectiveRequestContext: ManagementRequestContext | null = null;

export function setPerspectiveRequestContext(context: ManagementRequestContext): void {
  perspectiveRequestContext = context;
}

export function clearPerspectiveRequestContext(): void {
  perspectiveRequestContext = null;
}

type TokenSubscriber = () => void;
let tokenSubscribers: TokenSubscriber[] = [];

function readTokens(): void {
  if (typeof window === "undefined") return;
  accessToken = window.localStorage.getItem(ACCESS_TOKEN_KEY);
  refreshToken = window.localStorage.getItem(REFRESH_TOKEN_KEY);
}

readTokens();

function notifyTokenSubscribers(): void {
  for (const subscriber of tokenSubscribers) subscriber();
}

export function subscribeTokens(subscriber: TokenSubscriber): () => void {
  tokenSubscribers.push(subscriber);
  return () => {
    tokenSubscribers = tokenSubscribers.filter((fn) => fn !== subscriber);
  };
}

export function hasTokens(): boolean {
  return Boolean(accessToken && refreshToken);
}

export function setTokens(access: string, refresh: string): void {
  accessToken = access;
  refreshToken = refresh;
  if (typeof window !== "undefined") {
    window.localStorage.setItem(ACCESS_TOKEN_KEY, access);
    window.localStorage.setItem(REFRESH_TOKEN_KEY, refresh);
  }
  notifyTokenSubscribers();
}

export function clearTokens(): void {
  accessToken = null;
  refreshToken = null;
  if (typeof window !== "undefined") {
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    window.localStorage.removeItem(REFRESH_TOKEN_KEY);
  }
  notifyTokenSubscribers();
}

function getRefreshToken(): string | null {
  return refreshToken;
}

let refreshPromise: Promise<boolean> | null = null;

async function refreshAccessToken(): Promise<boolean> {
  const currentRefreshToken = getRefreshToken();
  if (!currentRefreshToken) return false;

  if (refreshPromise) return refreshPromise;

  refreshPromise = (async () => {
    try {
      const response = await fetch(joinPath(config.apiBaseUrl, endpoints.auth.refresh), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: currentRefreshToken }),
      });

      if (!response.ok) return false;

      const data = (await response.json()) as AuthResponse;
      setTokens(data.token, data.refreshToken);
      return true;
    } catch {
      return false;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

function isAuthEndpoint(path: string): boolean {
  return path.includes(endpoints.auth.login) || path.includes(endpoints.auth.refresh);
}

function isWorkItemEndpoint(path: string): boolean {
  return path === endpoints.workItems.root || path.startsWith(`${endpoints.workItems.root}/`);
}

function isMeetingEndpoint(path: string): boolean {
  return path === endpoints.meetings.root || path.startsWith(`${endpoints.meetings.root}/`);
}

function handleSessionExpired(): void {
  clearTokens();
  if (typeof window !== "undefined") {
    const current = window.location.pathname;
    if (!current.endsWith("/login")) {
      const locale = current.split("/")[1] ?? "en";
      window.location.assign(`/${locale}/login`);
    }
  }
}

function joinPath(base: string, path: string): string {
  return `${base.replace(/\/+$/, "")}/${path.replace(/^\/+/, "")}`;
}

function buildUrl(path: string, params?: Record<string, string | number | boolean | undefined> | PaginationParams | undefined): string {
  const url = new URL(joinPath(config.apiBaseUrl, path));
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined) url.searchParams.set(key, String(value));
    }
  }
  return url.toString();
}

/** Normalises an ASP.NET `ValidationProblemDetails.errors` map. */
function readFieldErrors(data: unknown): Record<string, string[]> {
  const errors = (data as { errors?: unknown } | undefined)?.errors;
  if (!errors || typeof errors !== "object") return {};

  return Object.entries(errors as Record<string, unknown>).reduce<Record<string, string[]>>(
    (accumulator, [field, messages]) => {
      const list = Array.isArray(messages)
        ? messages.filter((entry): entry is string => typeof entry === "string")
        : typeof messages === "string"
          ? [messages]
          : [];
      if (list.length) accumulator[field] = list;
      return accumulator;
    },
    {},
  );
}

/** Turns a model-binding path such as `$.baseCalendarId` into `baseCalendarId`. */
function toFieldName(path: string): string {
  return path
    .replace(/^\$\.?/, "")
    .replace(/\[[^\]]*\]/g, "")
    .replace(/\..*$/, "");
}

async function toApiError(response: Response): Promise<ApiError> {
  let data: unknown;
  try {
    data = await response.json();
  } catch {
    data = undefined;
  }

  const fieldErrors = Object.entries(readFieldErrors(data)).reduce<Record<string, string[]>>(
    (accumulator, [field, messages]) => {
      const name = toFieldName(field);
      if (!name) return accumulator;
      accumulator[name] = [...(accumulator[name] ?? []), ...messages];
      return accumulator;
    },
    {},
  );

  // Model-binding 400s carry a generic `title` ("One or more validation errors
  // occurred.") plus the real cause in `errors`, so prefer the field detail.
  const fieldMessages = Object.values(fieldErrors).flat();
  const message =
    (data as { message?: string } | undefined)?.message ??
    (data as { error?: string } | undefined)?.error ??
    (fieldMessages.length ? fieldMessages.join(" ") : undefined) ??
    (data as { title?: string } | undefined)?.title ??
    response.statusText;

  return new ApiError(response.status, message, data, fieldErrors);
}

async function request<T>(path: string, options: RequestOptions, alreadyRetried = false): Promise<T> {
  const isFormData = typeof FormData !== "undefined" && options.body instanceof FormData;
  const headers: Record<string, string> = { ...options.headers };
  if (!isFormData) headers["Content-Type"] = "application/json";

  if (accessToken) headers["Authorization"] = `Bearer ${accessToken}`;
  if (perspectiveRequestContext) {
    headers["X-Nexus-Perspective"] = perspectiveRequestContext.perspective;
    headers["X-Nexus-View-Mode"] = perspectiveRequestContext.viewMode;
    if (perspectiveRequestContext.portfolioId) headers["X-Nexus-Portfolio-Id"] = perspectiveRequestContext.portfolioId;
    if (perspectiveRequestContext.programId) headers["X-Nexus-Program-Id"] = perspectiveRequestContext.programId;
    if (perspectiveRequestContext.projectId) headers["X-Nexus-Project-Id"] = perspectiveRequestContext.projectId;
  }

  const url = buildUrl(path, options.params);

  let response: Response;
  try {
    response = await fetch(url, {
      method: options.method,
      headers,
      body: options.body !== undefined ? (isFormData ? options.body as FormData : JSON.stringify(options.body)) : undefined,
    });
  } catch {
    throw new ApiError(0, "Network error");
  }

  if (response.status === 401 && !alreadyRetried && !isAuthEndpoint(path) && getRefreshToken()) {
    const refreshed = await refreshAccessToken();
    if (refreshed) return request<T>(path, options, true);
    handleSessionExpired();
  }

  // Permission changes are reflected in newly issued JWTs. Retry protected
  // module requests once so an existing session picks up newly seeded role
  // permissions. A genuine denial remains a 403 after the retry and is not
  // bypassed.
  if (response.status === 403 && !alreadyRetried && (isWorkItemEndpoint(path) || isMeetingEndpoint(path)) && getRefreshToken()) {
    const refreshed = await refreshAccessToken();
    if (refreshed) return request<T>(path, options, true);
  }

  if (!response.ok) throw await toApiError(response);

  if (response.status === 204) return undefined as T;
  if (options.responseType === "blob") return (await response.blob()) as T;
  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string, params?: Record<string, string | number | boolean | undefined>, headers?: Record<string, string>) =>
    request<T>(path, { method: "GET", params, headers }),

  post: <T>(path: string, body?: unknown, headers?: Record<string, string>) =>
    request<T>(path, { method: "POST", body, headers }),

  upload: <T>(path: string, body: FormData) =>
    request<T>(path, { method: "POST", body }),

  download: <T = Blob>(path: string) =>
    request<T>(path, { method: "GET", responseType: "blob" }),

  put: <T>(path: string, body?: unknown, headers?: Record<string, string>) =>
    request<T>(path, { method: "PUT", body, headers }),

  patch: <T>(path: string, body?: unknown, headers?: Record<string, string>) =>
    request<T>(path, { method: "PATCH", body, headers }),

  delete: <T>(path: string, headers?: Record<string, string>, body?: unknown) =>
    request<T>(path, { method: "DELETE", headers, body }),

  login: (email: string, password: string) => api.post<AuthResponse>(endpoints.auth.login, { email, password }),

  logout: (token: string | null) => api.post<void>(endpoints.auth.logout, token ? { refreshToken: token } : {}),

  refresh: (token: string) => api.post<AuthResponse>(endpoints.auth.refresh, { refreshToken: token }),

  getMe: () => api.get<CurrentUser>(endpoints.auth.me),
  getPerspectives: () => api.get<PerspectiveSnapshot>(endpoints.auth.perspectives),
};



