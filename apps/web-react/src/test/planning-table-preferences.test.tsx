import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { planningApiFull, usePlanningTablePreferencesFull, useResetPlanningTablePreferencesFull, useSavePlanningTablePreferencesFull, type PlanningTablePreferenceDto } from "@features/projects/api/planningFull";

const projectId = "project-1";
const planId = "plan-1";
const preference: PlanningTablePreferenceDto = {
  planId,
  visibleColumns: ["schedulingMode", "id", "taskName", "description"],
  columnOrder: ["schedulingMode", "id", "taskName", "description"],
  columnWidths: { description: 312 },
  columnFilters: { taskName: { text: "release" } },
  collapsedRowIds: ["wbs-1"],
};
const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;

afterEach(() => { vi.restoreAllMocks(); queryClient.clear(); });

describe("planning table preference API hooks", () => {
  it("loads the current user's saved plan layout and auxiliary settings", async () => {
    vi.spyOn(planningApiFull, "tablePreferences").mockResolvedValue(preference);
    const { result } = renderHook(() => usePlanningTablePreferencesFull(projectId, planId), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(preference);
    expect(planningApiFull.tablePreferences).toHaveBeenCalledWith(projectId, planId);
  });

  it("saves the column layout through the existing authenticated endpoint", async () => {
    const saved = vi.spyOn(planningApiFull, "saveTablePreferences").mockResolvedValue(preference);
    const { result } = renderHook(() => useSavePlanningTablePreferencesFull(projectId, planId), { wrapper });
    const body = {
      visibleColumns: preference.visibleColumns,
      columnOrder: preference.columnOrder,
      columnWidths: preference.columnWidths,
      columnFilters: preference.columnFilters,
      collapsedRowIds: preference.collapsedRowIds,
    };

    await act(async () => { await result.current.mutateAsync(body); });
    expect(saved).toHaveBeenCalledWith(projectId, planId, body);
  });

  it("resets only the current user's saved layout for the selected plan", async () => {
    const reset = vi.spyOn(planningApiFull, "resetTablePreferences").mockResolvedValue(undefined);
    const { result } = renderHook(() => useResetPlanningTablePreferencesFull(projectId, planId), { wrapper });

    await act(async () => { await result.current.mutateAsync(); });
    expect(reset).toHaveBeenCalledWith(projectId, planId);
  });
});
