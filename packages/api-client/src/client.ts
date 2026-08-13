import type { PagedResult, PaginationParams } from "@nexus/shared-types";

export type { PaginationParams };

export type RequestInterceptor = (config: RequestConfig) => RequestConfig | Promise<RequestConfig>;
export type ResponseInterceptor = (response: Response) => Response | Promise<Response>;

export interface RequestConfig {
  method: string;
  url: string;
  headers: Record<string, string>;
  body?: unknown;
  params?: Record<string, string | number | boolean | undefined>;
}

export interface ApiClientOptions {
  baseUrl: string;
  authToken?: string;
  refreshTokenFn?: () => Promise<string>;
  requestInterceptors?: readonly RequestInterceptor[];
  responseInterceptors?: readonly ResponseInterceptor[];
}

export class ApiClient {
  private baseUrl: string;
  private authToken?: string;
  private refreshTokenFn?: () => Promise<string>;
  private requestInterceptors: RequestInterceptor[];
  private responseInterceptors: ResponseInterceptor[];

  constructor(options: ApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.authToken = options.authToken;
    this.refreshTokenFn = options.refreshTokenFn;
    this.requestInterceptors = [...(options.requestInterceptors ?? [])];
    this.responseInterceptors = [...(options.responseInterceptors ?? [])];
  }

  setAuthToken(token: string): void {
    this.authToken = token;
  }

  clearAuthToken(): void {
    this.authToken = undefined;
  }

  private buildUrl(path: string, params?: Record<string, string | number | boolean | undefined>): string {
    const url = new URL(`${this.baseUrl}${path}`);
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value !== undefined) {
          url.searchParams.set(key, String(value));
        }
      }
    }
    return url.toString();
  }

  private async request<T>(config: RequestConfig): Promise<T> {
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
      ...config.headers,
    };

    if (this.authToken) {
      headers["Authorization"] = `Bearer ${this.authToken}`;
    }

    let finalConfig: RequestConfig = { ...config, headers };

    for (const interceptor of this.requestInterceptors) {
      finalConfig = await interceptor(finalConfig);
    }

    const url = this.buildUrl(finalConfig.url, finalConfig.params);

    let response = await fetch(url, {
      method: finalConfig.method,
      headers: finalConfig.headers,
      body: finalConfig.body ? JSON.stringify(finalConfig.body) : undefined,
    });

    for (const interceptor of this.responseInterceptors) {
      response = await interceptor(response);
    }

    if (!response.ok) {
      const errorBody = await response.text();
      let errorData: Record<string, unknown> | undefined;
      try {
        errorData = JSON.parse(errorBody);
      } catch {
        // not JSON
      }

      if (response.status === 401 && this.refreshTokenFn) {
        const newToken = await this.refreshTokenFn();
        this.setAuthToken(newToken);
        return this.request<T>(config);
      }

      throw new ApiError(response.status, errorData?.message as string ?? response.statusText, errorData);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return response.json() as Promise<T>;
  }

  get<T>(path: string, params?: Record<string, string | number | boolean | undefined>, headers?: Record<string, string>): Promise<T> {
    return this.request<T>({ method: "GET", url: path, params, headers: headers ?? {} });
  }

  post<T>(path: string, body?: unknown, headers?: Record<string, string>): Promise<T> {
    return this.request<T>({ method: "POST", url: path, body, headers: headers ?? {} });
  }

  put<T>(path: string, body?: unknown, headers?: Record<string, string>): Promise<T> {
    return this.request<T>({ method: "PUT", url: path, body, headers: headers ?? {} });
  }

  delete<T>(path: string, headers?: Record<string, string>): Promise<T> {
    return this.request<T>({ method: "DELETE", url: path, headers: headers ?? {} });
  }

  getPaged<T>(path: string, params?: PaginationParams & Record<string, string | number | boolean | undefined>): Promise<PagedResult<T>> {
    return this.get<PagedResult<T>>(path, params as Record<string, string | number | boolean | undefined>);
  }
}

export class ApiError extends Error {
  status: number;
  data?: Record<string, unknown>;

  constructor(status: number, message: string, data?: Record<string, unknown>) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.data = data;
  }
}
