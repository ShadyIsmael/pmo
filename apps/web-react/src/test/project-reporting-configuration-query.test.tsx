import type { PropsWithChildren } from "react";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { clearTokens, setTokens } from "../api/client";
import { useProjectReportingConfiguration } from "../features/projects/api/projects";

function createWrapper(client: QueryClient) {
  return function Wrapper({ children }: PropsWithChildren) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

function createQueryClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

describe("project reporting configuration query", () => {
  beforeEach(() => {
    window.localStorage.clear();
    setTokens("test-access-token", "test-refresh-token");
  });

  afterEach(() => {
    clearTokens();
    vi.unstubAllGlobals();
  });

  it("normalizes an empty 204 response to the no-configuration value", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useProjectReportingConfiguration("project-1"), {
      wrapper: createWrapper(createQueryClient()),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
    expect(result.current.isError).toBe(false);
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it("keeps an explicit JSON null response as the no-configuration value", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response("null", { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const { result } = renderHook(() => useProjectReportingConfiguration("project-1"), {
      wrapper: createWrapper(createQueryClient()),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
    expect(result.current.isError).toBe(false);
  });
});
