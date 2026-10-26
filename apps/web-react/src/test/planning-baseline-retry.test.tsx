import type { PropsWithChildren } from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import { createSessionQueryClient } from "../api/queryClient";
import { planningApiFull, usePlanningFullMutations } from "../features/projects/api/planningFull";

afterEach(() => vi.restoreAllMocks());

describe("plan baseline capture retries", () => {
  it("does not replay the baseline POST after a server error", async () => {
    const request = vi.spyOn(planningApiFull, "captureBaseline").mockRejectedValue(
      new ApiError(500, "Internal Server Error"),
    );
    const queryClient = createSessionQueryClient();
    const wrapper = ({ children }: PropsWithChildren) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
    const { result } = renderHook(() => usePlanningFullMutations("project-1", "plan-1"), { wrapper });

    await act(async () => {
      await expect(result.current.captureBaseline.mutateAsync("working-row-version"))
        .rejects.toThrow("Internal Server Error");
    });

    expect(request).toHaveBeenCalledOnce();
    queryClient.clear();
  });
});
