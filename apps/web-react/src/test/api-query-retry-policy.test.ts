import { describe, expect, it } from "vitest";
import { ApiError } from "@api/client";
import { createSessionQueryClient } from "@api/queryClient";

describe("session query retry policy", () => {
  it.each([400, 401, 403])("does not retry a deterministic HTTP %i response", (status) => {
    const queryClient = createSessionQueryClient();
    const retry = queryClient.getDefaultOptions().queries?.retry;

    expect(typeof retry).toBe("function");
    expect((retry as (failureCount: number, error: unknown) => boolean)(0, new ApiError(status, "denied"))).toBe(false);
    queryClient.clear();
  });

  it("retries one transient server or network failure, then stops", () => {
    const queryClient = createSessionQueryClient();
    const retry = queryClient.getDefaultOptions().queries?.retry as (failureCount: number, error: unknown) => boolean;

    expect(retry(0, new ApiError(503, "temporarily unavailable"))).toBe(true);
    expect(retry(1, new ApiError(503, "temporarily unavailable"))).toBe(false);
    expect(retry(0, new TypeError("network unavailable"))).toBe(true);
    expect(retry(1, new TypeError("network unavailable"))).toBe(false);
    queryClient.clear();
  });
});
