import { useQueryClient } from "@tanstack/react-query";
import { usePlanningFullMutations, planningKeys, type PlanActivityDto, type PlanWbsNodeDto } from "./planningFull";

function upsertActivity(items: PlanActivityDto[] | undefined, updated: PlanActivityDto): PlanActivityDto[] {
  if (!items) return [updated];
  return items.some((item) => item.id === updated.id)
    ? items.map((item) => item.id === updated.id ? updated : item)
    : [...items, updated];
}

function upsertWbsNode(nodes: PlanWbsNodeDto[] | undefined, updated: PlanWbsNodeDto): PlanWbsNodeDto[] {
  const current = nodes ?? [];
  const find = (items: PlanWbsNodeDto[]): PlanWbsNodeDto | undefined => {
    for (const node of items) {
      if (node.id === updated.id) return node;
      const child = find(node.children ?? []);
      if (child) return child;
    }
    return undefined;
  };
  const existing = find(current);
  const moved = { ...existing, ...updated, children: updated.children?.length ? updated.children : existing?.children ?? [] };
  const remove = (items: PlanWbsNodeDto[]): PlanWbsNodeDto[] => items
    .filter((node) => node.id !== updated.id)
    .map((node) => ({ ...node, children: remove(node.children ?? []) }));
  const order = (items: PlanWbsNodeDto[]) => items.sort((left, right) => left.sortOrder - right.sortOrder || left.code.localeCompare(right.code));
  const remaining = remove(current);
  if (!updated.parentWbsId) return order([...remaining, moved]);

  let parentFound = false;
  const addToParent = (items: PlanWbsNodeDto[]): PlanWbsNodeDto[] => items.map((node) => {
    if (node.id === updated.parentWbsId) {
      parentFound = true;
      return { ...node, children: order([...(node.children ?? []), moved]) };
    }
    const children = addToParent(node.children ?? []);
    return children === node.children ? node : { ...node, children };
  });
  const nested = addToParent(remaining);
  return parentFound ? nested : remaining;
}

function removeWbsNode(nodes: PlanWbsNodeDto[] | undefined, id: string): PlanWbsNodeDto[] | undefined {
  return nodes?.filter((node) => node.id !== id).map((node) => ({ ...node, children: removeWbsNode(node.children, id) ?? [] }));
}

export function usePlanningTableMutations(projectId: string, planId?: string) {
  const client = useQueryClient();
  const mutations = usePlanningFullMutations(projectId, planId);
  const activityKey = planId ? planningKeys.activities(projectId, planId) : undefined;
  const wbsKey = planId ? planningKeys.wbs(projectId, planId) : undefined;

  const createActivity = {
    ...mutations.createActivity,
    mutateAsync: async (body: unknown) => {
      const created = await mutations.createActivity.mutateAsync(body);
      if (activityKey) client.setQueryData<PlanActivityDto[]>(activityKey, (current) => upsertActivity(current, created as PlanActivityDto));
      return created;
    },
  };
  const updateActivity = {
    ...mutations.updateActivity,
    mutateAsync: async (input: { id: string; body: unknown }) => {
      const updated = await mutations.updateActivity.mutateAsync(input);
      if (activityKey) client.setQueryData<PlanActivityDto[]>(activityKey, (current) => current?.map((item) => item.id === input.id ? updated as PlanActivityDto : item));
      if (planId) await Promise.all([
        client.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) }),
        client.invalidateQueries({ queryKey: planningKeys.wbs(projectId, planId) }),
        client.invalidateQueries({ queryKey: planningKeys.dependencies(projectId, planId) }),
      ]);
      return updated;
    },
  };
  const deleteActivity = {
    ...mutations.deleteActivity,
    mutateAsync: async (input: { id: string; rowVersion: string }) => {
      const result = await mutations.deleteActivity.mutateAsync(input);
      if (activityKey) client.setQueryData<PlanActivityDto[]>(activityKey, (current) => current?.filter((item) => item.id !== input.id));
      return result;
    },
  };
  const createWbs = {
    ...mutations.createWbs,
    mutateAsync: async (body: unknown) => {
      const created = await mutations.createWbs.mutateAsync(body);
      if (wbsKey) client.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => upsertWbsNode(current, created as PlanWbsNodeDto));
      return created;
    },
  };
  const updateWbs = {
    ...mutations.updateWbs,
    mutateAsync: async (input: { id: string; body: unknown }) => {
      const updated = await mutations.updateWbs.mutateAsync(input);
      if (wbsKey) client.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => upsertWbsNode(current, updated as PlanWbsNodeDto));
      if (planId) await Promise.all([
        client.invalidateQueries({ queryKey: planningKeys.wbs(projectId, planId) }),
        client.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) }),
      ]);
      return updated;
    },
  };
  const deleteWbs = {
    ...mutations.deleteWbs,
    mutateAsync: async (input: { id: string; rowVersion: string }) => {
      const result = await mutations.deleteWbs.mutateAsync(input);
      if (wbsKey) client.setQueryData<PlanWbsNodeDto[]>(wbsKey, (current) => removeWbsNode(current, input.id));
      return result;
    },
  };

  const moveActivity = {
    ...mutations.moveActivity,
    mutateAsync: async (input: Parameters<typeof mutations.moveActivity.mutateAsync>[0]) => {
      const moved = await mutations.moveActivity.mutateAsync(input);
      if (planId) {
        await Promise.all([
          client.invalidateQueries({ queryKey: planningKeys.activities(projectId, planId) }),
          client.invalidateQueries({ queryKey: planningKeys.wbs(projectId, planId) }),
          client.invalidateQueries({ queryKey: planningKeys.dependencies(projectId, planId) }),
        ]);
      }
      return moved;
    },
  };

  return { ...mutations, createActivity, updateActivity, deleteActivity, createWbs, updateWbs, deleteWbs, moveActivity };
}
