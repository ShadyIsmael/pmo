export type PlanSelection = {
  include: boolean;
  workingVersionId: string;
  baselineVersionId: string;
};

export function updatePlanSelection(
  current: ReadonlyMap<string, PlanSelection>,
  planId: string,
  patch: Partial<PlanSelection>,
): Map<string, PlanSelection> {
  const next = new Map(current);
  next.set(planId, {
    ...(current.get(planId) ?? { include: false, workingVersionId: "", baselineVersionId: "" }),
    ...patch,
  });
  return next;
}
