import { config } from "@/lib/config";
import { endpoints } from "@/lib/api/endpoints";

const ACCESS_TOKEN_KEY = "nexus_access_token";
const REFRESH_TOKEN_KEY = "nexus_refresh_token";

export class ApiError extends Error {
  status: number;
  data?: unknown;

  constructor(status: number, message: string, data?: unknown) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.data = data;
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
}

let accessToken: string | null = null;
let refreshToken: string | null = null;

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

async function toApiError(response: Response): Promise<ApiError> {
  let data: unknown;
  try {
    data = await response.json();
  } catch {
    data = undefined;
  }
  const message =
    (data as { message?: string } | undefined)?.message ??
    (data as { title?: string } | undefined)?.title ??
    response.statusText;
  return new ApiError(response.status, message, data);
}

async function request<T>(path: string, options: RequestOptions, alreadyRetried = false): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...options.headers,
  };

  if (accessToken) headers["Authorization"] = `Bearer ${accessToken}`;

  const url = buildUrl(path, options.params);

  let response: Response;
  try {
    response = await fetch(url, {
      method: options.method,
      headers,
      body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    });
  } catch {
    throw new ApiError(0, "Network error");
  }

  if (response.status === 401 && !alreadyRetried && !isAuthEndpoint(path) && getRefreshToken()) {
    const refreshed = await refreshAccessToken();
    if (refreshed) return request<T>(path, options, true);
    handleSessionExpired();
  }

  if (!response.ok) throw await toApiError(response);

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string, params?: Record<string, string | number | boolean | undefined>, headers?: Record<string, string>) =>
    request<T>(path, { method: "GET", params, headers }),

  post: <T>(path: string, body?: unknown, headers?: Record<string, string>) =>
    request<T>(path, { method: "POST", body, headers }),

  put: <T>(path: string, body?: unknown, headers?: Record<string, string>) =>
    request<T>(path, { method: "PUT", body, headers }),

  delete: <T>(path: string, headers?: Record<string, string>) =>
    request<T>(path, { method: "DELETE", headers }),

  login: (email: string, password: string) => api.post<AuthResponse>(endpoints.auth.login, { email, password }),

  logout: (token: string | null) => api.post<void>(endpoints.auth.logout, token ? { refreshToken: token } : {}),

  refresh: (token: string) => api.post<AuthResponse>(endpoints.auth.refresh, { refreshToken: token }),

  getMe: () => api.get<CurrentUser>(endpoints.auth.me),
};



