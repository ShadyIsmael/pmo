import type { PagedResult, RiskDto } from "@/types/risk";

const API_BASE = "/api";

let accessToken: string | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

async function request<T>(
  path: string,
  options: { method: string; body?: unknown; params?: Record<string, string | number | boolean | undefined> }
): Promise<T> {
  const url = new URL(`${API_BASE}${path}`, window.location.origin);
  if (options.params) {
    for (const [key, value] of Object.entries(options.params)) {
      if (value !== undefined) url.searchParams.set(key, String(value));
    }
  }

  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (accessToken) headers["Authorization"] = `Bearer ${accessToken}`;

  const response = await fetch(url.toString(), {
    method: options.method,
    headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });

  if (!response.ok) {
    const data = await response.json().catch(() => null);
    throw new Error(data?.message ?? response.statusText);
  }

  if (response.status === 204) return undefined as T;
  return response.json();
}

export const riskApi = {
  list: (params?: { projectId?: string; page?: number; pageSize?: number }) =>
    request<PagedResult<RiskDto>>(
      "/risks",
      { method: "GET", params }
    ),
};
