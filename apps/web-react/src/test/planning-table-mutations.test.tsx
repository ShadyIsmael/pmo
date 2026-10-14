import type { PropsWithChildren } from "react";
import { act, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, expect, it, vi } from "vitest";
import { planningApiFull, planningKeys, type PlanActivityDto, type PlanWbsNodeDto } from "@features/projects/api/planningFull";
import { usePlanningTableMutations } from "@features/projects/api/planning-table-mutations";

function createClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: PropsWithChildren) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

function activity(overrides: Partial<PlanActivityDto> = {}): PlanActivityDto {
  return { id: "activity-1", planVersionId: "plan-1", code: "T-1", name: "Task one", type: "Task", duration: 1, critical: false, rowVersion: "row-v1", wbsId: "wbs-1", ...overrides };
}

function wbsNode(overrides: Partial<PlanWbsNodeDto> = {}): PlanWbsNodeDto {
  return { id: "wbs-1", planVersionId: "plan-1", sortOrder: 1, code: "WBS-1", name: "Task one", activityId: "activity-1", rowVersion: "wbs-v1", children: [], ...overrides };
}

afterEach(() => vi.restoreAllMocks());

describe("planning ribbon and table mutation synchronization", () => {
  it("applies saved formatting to the cached activity and WBS row before a list refetch", async () => {
    const client = createClient();
    const activityKey = planningKeys.activities("project-1", "plan-1");
    const wbsKey = planningKeys.wbs("project-1", "plan-1");
    const currentActivity = activity();
    const currentNode = wbsNode({ children: [wbsNode({ id: "wbs-child", code: "WBS-CHILD", name: "Child", activityId: "activity-child" })] });
    const updatedActivity = activity({ textFormat: { isBold: true, isItalic: false, isUnderline: false }, rowVersion: "row-v2" });
    const updatedNode = wbsNode({ textFormat: { isBold: true, isItalic: false, isUnderline: false }, rowVersion: "wbs-v2" });
    client.setQueryData(activityKey, [currentActivity]);
    client.setQueryData(wbsKey, [currentNode]);
    vi.spyOn(planningApiFull, "updateActivity").mockResolvedValue(updatedActivity);
    vi.spyOn(planningApiFull, "updateWbs").mockResolvedValue(updatedNode);
    const { result } = renderHook(() => usePlanningTableMutations("project-1", "plan-1"), { wrapper: wrapperFor(client) });

    await act(async () => {
      await result.current.updateActivity.mutateAsync({ id: currentActivity.id, body: { textFormat: updatedActivity.textFormat } });
      await result.current.updateWbs.mutateAsync({ id: currentNode.id, body: { textFormat: updatedNode.textFormat } });
    });

    expect(client.getQueryData<PlanActivityDto[]>(activityKey)?.[0]).toMatchObject({ textFormat: { isBold: true }, rowVersion: "row-v2" });
    expect(client.getQueryData<PlanWbsNodeDto[]>(wbsKey)?.[0]).toMatchObject({ textFormat: { isBold: true }, rowVersion: "wbs-v2" });
    expect(client.getQueryData<PlanWbsNodeDto[]>(wbsKey)?.[0].children).toHaveLength(1);
  });

  it("inserts and removes ribbon-created task rows in the current table cache", async () => {
    const client = createClient();
    const activityKey = planningKeys.activities("project-1", "plan-1");
    const wbsKey = planningKeys.wbs("project-1", "plan-1");
    const createdNode = wbsNode({ id: "wbs-new", code: "T-NEW", name: "New task", activityId: undefined, parentWbsId: undefined });
    const createdActivity = activity({ id: "activity-new", code: "T-NEW", name: "New task", wbsId: createdNode.id });
    client.setQueryData<PlanActivityDto[]>(activityKey, []);
    client.setQueryData<PlanWbsNodeDto[]>(wbsKey, []);
    vi.spyOn(planningApiFull, "createWbs").mockResolvedValue(createdNode);
    vi.spyOn(planningApiFull, "createActivity").mockResolvedValue(createdActivity);
    vi.spyOn(planningApiFull, "deleteActivity").mockResolvedValue(undefined);
    vi.spyOn(planningApiFull, "deleteWbs").mockResolvedValue(undefined);
    const { result } = renderHook(() => usePlanningTableMutations("project-1", "plan-1"), { wrapper: wrapperFor(client) });

    await act(async () => {
      await result.current.createWbs.mutateAsync({ code: createdNode.code, name: createdNode.name });
      await result.current.createActivity.mutateAsync({ code: createdActivity.code, name: createdActivity.name });
    });
    expect(client.getQueryData<PlanActivityDto[]>(activityKey)).toEqual([createdActivity]);
    expect(client.getQueryData<PlanWbsNodeDto[]>(wbsKey)).toEqual([createdNode]);

    await act(async () => {
      await result.current.deleteActivity.mutateAsync({ id: createdActivity.id, rowVersion: createdActivity.rowVersion });
      await result.current.deleteWbs.mutateAsync({ id: createdNode.id, rowVersion: createdNode.rowVersion });
    });
    expect(client.getQueryData<PlanActivityDto[]>(activityKey)).toEqual([]);
    expect(client.getQueryData<PlanWbsNodeDto[]>(wbsKey)).toEqual([]);
  });

  it("reparents a WBS row in cache without dropping its descendant hierarchy", async () => {
    const client = createClient();
    const wbsKey = planningKeys.wbs("project-1", "plan-1");
    const child = wbsNode({ id: "wbs-child", code: "WBS-C", name: "Child", activityId: undefined, parentWbsId: "wbs-moved" });
    const firstRoot = wbsNode({ id: "wbs-root-1", code: "WBS-1", activityId: undefined, children: [] });
    const moved = wbsNode({ id: "wbs-moved", code: "WBS-2", activityId: undefined, sortOrder: 1, children: [child] });
    client.setQueryData<PlanWbsNodeDto[]>(wbsKey, [firstRoot, moved]);
    const updatedMoved = { ...moved, parentWbsId: firstRoot.id, sortOrder: 0, rowVersion: "wbs-v2", children: [] };
    vi.spyOn(planningApiFull, "updateWbs").mockResolvedValue(updatedMoved);
    const { result } = renderHook(() => usePlanningTableMutations("project-1", "plan-1"), { wrapper: wrapperFor(client) });

    await act(async () => {
      await result.current.updateWbs.mutateAsync({ id: moved.id, body: { parentWbsId: firstRoot.id, sortOrder: 0 } });
    });

    const tree = client.getQueryData<PlanWbsNodeDto[]>(wbsKey)!;
    expect(tree).toHaveLength(1);
    expect(tree[0]).toMatchObject({ id: firstRoot.id, children: [{ id: moved.id, children: [{ id: child.id }] }] });
  });
});
