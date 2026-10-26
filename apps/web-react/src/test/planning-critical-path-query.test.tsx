import type { PropsWithChildren } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { planningApiFull, planningKeys, usePlanningActivitiesFull, usePlanningDependenciesFull, usePlanningFullMutations, usePlanningSchedulingAnalysisFull } from "@features/projects/api/planningFull";
import type { PlanActivityDto, PlanDependencyDto } from "@features/projects/api/planningFull";

function makeClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: PropsWithChildren) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

afterEach(() => vi.restoreAllMocks());

describe("critical path scheduling query", () => {
  it("does not request CPM data until the Network view enables the query", async () => {
    const client = makeClient();
    const request = vi.spyOn(planningApiFull, "schedulingAnalysis").mockResolvedValue({
      planVersionId: "version-1", hasCycle: false, criticalPathActivityIds: [], criticalPathDependencyIds: [], activities: [],
    });
    const { result, rerender } = renderHook(({ enabled }) => usePlanningSchedulingAnalysisFull("project-1", "plan-1", enabled), {
      initialProps: { enabled: false },
      wrapper: wrapperFor(client),
    });

    expect(request).not.toHaveBeenCalled();
    rerender({ enabled: true });
    await waitFor(() => expect(request).toHaveBeenCalledWith("project-1", "plan-1"));
    await waitFor(() => expect(result.current.data?.planVersionId).toBe("version-1"));
  });

  it("invalidates CPM data after saved task, dependency, and calendar changes", async () => {
    const client = makeClient();
    const analysisKey = planningKeys.schedulingAnalysis("project-1", "plan-1");
    const cachedAnalysis = { planVersionId: "version-1", hasCycle: false, criticalPathActivityIds: [], criticalPathDependencyIds: [], activities: [] };
    client.setQueryData(analysisKey, cachedAnalysis);
    vi.spyOn(planningApiFull, "updateActivity").mockResolvedValue({} as never);
    vi.spyOn(planningApiFull, "updateDependency").mockResolvedValue({} as never);
    vi.spyOn(planningApiFull, "updateCalendar").mockResolvedValue({} as never);
    const { result } = renderHook(() => usePlanningFullMutations("project-1", "plan-1"), { wrapper: wrapperFor(client) });

    await act(async () => {
      await result.current.updateActivity.mutateAsync({ id: "activity-1", body: { duration: 2 } });
    });
    await waitFor(() => expect(client.getQueryState(analysisKey)?.isInvalidated).toBe(true));

    client.setQueryData(analysisKey, cachedAnalysis);
    await act(async () => {
      await result.current.updateDependency.mutateAsync({ id: "dependency-1", body: { lag: 1 } });
    });
    await waitFor(() => expect(client.getQueryState(analysisKey)?.isInvalidated).toBe(true));

    client.setQueryData(analysisKey, cachedAnalysis);
    await act(async () => {
      await result.current.updateCalendar.mutateAsync({ id: "calendar-1", body: { name: "Updated calendar" } });
    });
    await waitFor(() => expect(client.getQueryState(analysisKey)?.isInvalidated).toBe(true));
  });

  it("refreshes the persisted grid, history, and critical path after schedule recalculation", async () => {
    const client = makeClient();
    const activityKey = planningKeys.activities("project-1", "plan-1");
    const historyKey = planningKeys.gridHistory("project-1", "plan-1");
    const analysisKey = planningKeys.schedulingAnalysis("project-1", "plan-1");
    client.setQueryData(activityKey, []);
    client.setQueryData(historyKey, { canUndo: false, canRedo: false });
    client.setQueryData(analysisKey, { planVersionId: "version-1", hasCycle: false, criticalPathActivityIds: [], criticalPathDependencyIds: [], activities: [] });
    const recalculate = vi.spyOn(planningApiFull, "recalculateSchedule").mockResolvedValue({ recalculatedActivityCount: 2, changedActivities: [] });
    const { result } = renderHook(() => usePlanningFullMutations("project-1", "plan-1"), { wrapper: wrapperFor(client) });

    await act(async () => {
      await result.current.recalculateSchedule.mutateAsync();
    });

    expect(recalculate).toHaveBeenCalledWith("project-1", "plan-1");
    await waitFor(() => expect(client.getQueryState(activityKey)?.isInvalidated).toBe(true));
    expect(client.getQueryState(historyKey)?.isInvalidated).toBe(true);
    expect(client.getQueryState(analysisKey)?.isInvalidated).toBe(true);
  });

  it("waits for recalculated activity dates to refresh after adding a predecessor", async () => {
    const client = makeClient();
    const initialActivities: PlanActivityDto[] = [
      { id: "predecessor", planVersionId: "version-1", code: "1", name: "Predecessor", type: "Task", duration: 2, startDate: "2026-10-05", endDate: "2026-10-07", critical: false, rowVersion: "v1" },
      { id: "successor", planVersionId: "version-1", code: "2", name: "Successor", type: "Task", duration: 1, startDate: "2026-10-01", endDate: "2026-10-02", critical: false, rowVersion: "v1" },
    ];
    const scheduledActivities = initialActivities.map((activity) => activity.id === "successor"
      ? { ...activity, startDate: "2026-10-07", endDate: "2026-10-08", rowVersion: "v2" }
      : activity);
    const dependency: PlanDependencyDto = {
      id: "dependency-1", planVersionId: "version-1", activityId: "successor", predecessorActivityId: "predecessor",
      type: "FinishToStart", lag: 0, rowVersion: "v1",
    };
    let finishActivityRefresh!: () => void;
    const activityRefreshGate = new Promise<void>((resolve) => { finishActivityRefresh = resolve; });
    let activityReads = 0;
    let dependencyReads = 0;
    vi.spyOn(planningApiFull, "activities").mockImplementation(async () => {
      activityReads += 1;
      if (activityReads === 1) return initialActivities;
      await activityRefreshGate;
      return scheduledActivities;
    });
    vi.spyOn(planningApiFull, "dependencies").mockImplementation(async () => {
      dependencyReads += 1;
      return dependencyReads === 1 ? [] : [dependency];
    });
    vi.spyOn(planningApiFull, "createDependency").mockResolvedValue(dependency);

    const activityQuery = renderHook(() => usePlanningActivitiesFull("project-1", "plan-1"), { wrapper: wrapperFor(client) });
    const dependencyQuery = renderHook(() => usePlanningDependenciesFull("project-1", "plan-1"), { wrapper: wrapperFor(client) });
    const mutations = renderHook(() => usePlanningFullMutations("project-1", "plan-1"), { wrapper: wrapperFor(client) });
    await waitFor(() => expect(activityQuery.result.current.data?.find((item) => item.id === "successor")?.startDate).toBe("2026-10-01"));
    await waitFor(() => expect(dependencyQuery.result.current.data).toEqual([]));

    let mutationSettled = false;
    let mutationPromise!: Promise<PlanDependencyDto>;
    act(() => {
      mutationPromise = mutations.result.current.createDependency.mutateAsync({ activityId: "successor", predecessorActivityId: "predecessor", type: "FinishToStart", lag: 0 });
      void mutationPromise.then(() => { mutationSettled = true; }, () => { mutationSettled = true; });
    });
    await waitFor(() => expect(activityReads).toBe(2));
    expect(mutationSettled).toBe(false);

    await act(async () => {
      finishActivityRefresh();
      await mutationPromise;
    });

    expect(mutationSettled).toBe(true);
    await waitFor(() => expect(activityQuery.result.current.data?.find((item) => item.id === "successor")?.startDate).toBe("2026-10-07"));
    expect(dependencyQuery.result.current.data).toEqual([dependency]);
  });
});
